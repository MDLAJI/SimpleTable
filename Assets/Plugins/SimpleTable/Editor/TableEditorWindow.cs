/*
 * 此文件基于原项目 HTFramework (https://github.com/SaiTingHu/HTFramework) 的代码修改
 * 原文件版权归原作者所有，遵循MIT许可证。
 * 
 * 原始版权声明：
 * Copyright (c) 2019 HuTao
 * 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UObject = UnityEngine.Object;
using SimpleTable.Runtime;

namespace SimpleTable.Editor
{
    /// <summary>
    /// 通用表格绘制器
    /// </summary>
    public sealed class TableEditorWindow : EditorWindow
    {
        private const int Border = 10;
        private const int TitleHeight = 20;
        private Dictionary<string, FieldInfo> _fieldInfos = new Dictionary<string, FieldInfo>();
        private TableView<object> _tableView;
        private UObject _target;
        private string _targetName;
        private bool _isAutoSave = true;

        private List<UObject> dataTableList = new List<UObject>();  // 数据表列表
        private string[] dataTableDisplayName;
        private int currentDataTableIndex = -1;

        /// <summary>
        /// 打开通用表格绘制器
        /// </summary>
        /// <param name="target">表格数据目标实例</param>
        /// <param name="fieldName">表格数据的字段名称</param>
        public static void OpenWindow(UObject target, string fieldName)
        {
            TableEditorWindow window = GetWindow<TableEditorWindow>();
            window.titleContent.image = EditorGUIUtility.IconContent("ScriptableObject Icon").image;
            window.titleContent.text = "表格编辑器";
            window.OnInit(target, fieldName);
        }

        [UnityEditor.Callbacks.OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int lineNumber)
        {
            // 打开DataTableBase的子类时打开表格编辑器
            var uobject = EditorUtility.InstanceIDToObject(instanceID);
            if (uobject is DataTableBase)
            {
                OpenWindow(uobject, SimpleTableConst.tableListFieldName);
                return true;
            }
            return false;
        }

        private void OnInit(UObject target, string fieldName)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo == null)
            {
                Debug.Log($"表格编辑器：未从 {target.GetType().FullName} 中找到字段 {fieldName}！");
                Close();
                return;
            }

            // 获取数据表的值
            List<object> datas = GetDatas(fieldInfo.GetValue(target));

            // 反射获取数据表结构
            FieldInfo f = target.GetType().GetField(SimpleTableConst.tableStructFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Type dataStruct = f.GetValue(target) as Type;

            // 通过数据表结构创建表头
            List<TableColumn<object>> columns = GetColumns(dataStruct, out int rowHeightMul);
            if (columns.Count <= 0)
            {
                Debug.Log($"表格编辑器：{target.GetType().FullName} 的字段 {fieldName} 不是复杂类型，或类型中不含有可序列化字段！");
                Close();
                return;
            }

            // 创建表格视图
            _tableView = new TableView<object>(datas, columns);
            _tableView.RowHeight *= rowHeightMul;
            _tableView.IsEnableContextClick = true;
            _tableView.IsEnableSearch = true;
            _tableView.tableDataStruct = dataStruct;
            _tableView.target = target;
            _target = target;
            _targetName = $"{_target.GetType().FullName}.{fieldName} ({_target.name})";

            // 反射获取所有字段的Tooltip
            var fields = dataStruct.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            List<string> tips = new List<string>();
            foreach (var item in fields)
            {
                TooltipAttribute tooltipAttr = item.GetCustomAttribute<TooltipAttribute>();
                tips.Add(tooltipAttr != null ? tooltipAttr.tooltip : "");
            }
            _tableView.ColumnToolTip = tips.ToArray();

            // 自动保存
            if (_isAutoSave) _tableView.AutoSaveAction = () => { SaveTableData(); };

            // 当前选中的数据表索引
            currentDataTableIndex = dataTableList.IndexOf(target);
        }


        private void OnEnable()
        {
            // 搜索所有的数据表
            var guids = AssetDatabase.FindAssets("t:DataTableBase");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                dataTableList.Add(so);
            }

            List<string> displayName = new List<string>();
            foreach (var item in dataTableList)
            {
                displayName.Add(item.name);
            }
            dataTableDisplayName = displayName.ToArray();
        }

        public void OnGUI()
        {
            DrawToolBar();
            GUILayout.Space(5);
            DrawSettingBar();

            Rect rect = new Rect(0, 0, position.width, position.height);
            rect.x += Border;
            rect.y += Border + TitleHeight + 20;
            rect.width -= Border * 2;
            rect.height -= Border * 2 + TitleHeight + 15;
            _tableView.OnGUI(rect);
            _tableView.DrawHeaderToolTips();
        }
        private void Update()
        {
            if (EditorApplication.isCompiling || _tableView == null || _target == null)
            {
                Close();
            }
        }

        /// <summary>
        /// 绘制工具栏
        /// </summary>
        private void DrawToolBar()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("选中表格", EditorStyles.toolbarButton))
            {
                Selection.activeObject = _target;
                EditorGUIUtility.PingObject(_target);
            }

            if (GUILayout.Button("导出为Excel", EditorStyles.toolbarButton))
            {
                DataTableExporter.ExportAsExcel(_target);
            }

            if (GUILayout.Button("导出为Json", EditorStyles.toolbarButton))
            {
                // TODO
            }

            if (GUILayout.Button("导出为Lua", EditorStyles.toolbarButton))
            {
                DataTableExporter.ExportAsLua(_target);
            }

            if (GUILayout.Button("保存", EditorStyles.toolbarButton))
            {
                SaveTableData();
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制设置栏
        /// </summary>
        private void DrawSettingBar()
        {
            GUILayout.BeginHorizontal();
            int oldIndex = currentDataTableIndex;
            currentDataTableIndex = EditorGUILayout.Popup(currentDataTableIndex, dataTableDisplayName, GUILayout.Width(200f));
            if (oldIndex != currentDataTableIndex)
            {
                OnInit(dataTableList[currentDataTableIndex], SimpleTableConst.tableListFieldName);
            }
            GUILayout.Label($"{_target.GetType().FullName}({_target.name})");
            bool oldAutoSave = _isAutoSave;
            _isAutoSave = GUILayout.Toggle(_isAutoSave, "自动保存");
            if (oldAutoSave != _isAutoSave)
            {
                if (_isAutoSave)
                {
                    _tableView.AutoSaveAction = () => { SaveTableData(); };
                }
                else
                {
                    _tableView.AutoSaveAction = null;
                }
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 获取列表或数组字段的值
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        private List<object> GetDatas(object field)
        {
            List<object> datas = new List<object>();
            Array array = field as Array;
            IEnumerable<object> list = field as IEnumerable<object>;
            if (array != null)
            {
                foreach (var item in array)
                {
                    datas.Add(item);
                }
            }
            else if (list != null)
            {
                foreach (var item in list)
                {
                    datas.Add(item);
                }
            }
            return datas;
        }

        /// <summary>
        /// 获取各个类型的列视图
        /// </summary>
        /// <param name="type">表格结构类型</param>
        /// <param name="rowHeightMul">所需行高相较于原始行高的倍率</param>
        private List<TableColumn<object>> GetColumns(Type type, out int rowHeightMul)
        {
            rowHeightMul = 1;
            _fieldInfos.Clear();
            FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                if (fieldInfos[i].IsPublic || fieldInfos[i].IsDefined(typeof(SerializeField), true))
                {
                    if (!_fieldInfos.ContainsKey(fieldInfos[i].Name))
                    {
                        _fieldInfos.Add(fieldInfos[i].Name, fieldInfos[i]);
                    }
                }
            }

            List<TableColumn<object>> columns = new List<TableColumn<object>>();
            foreach (var item in _fieldInfos)
            {
                TableColumn<object> column = null;
                FieldInfo field = item.Value;
                if (field.FieldType.IsEnum)
                {
                    column = GetEnumColumn(field);
                }
                else if (field.FieldType == typeof(string))
                {
                    column = GetStringColumn(field);
                }
                else if (field.FieldType == typeof(int))
                {
                    column = GetIntColumn(field);
                }
                else if (field.FieldType == typeof(long))
                {
                    column = GetLongColumn(field);
                }
                else if (field.FieldType == typeof(float))
                {
                    column = GetFloatColumn(field);
                }
                else if (field.FieldType == typeof(double))
                {
                    column = GetDoubleColumn(field);
                }
                else if (field.FieldType == typeof(bool))
                {
                    column = GetBoolColumn(field);
                }
                else if (field.FieldType == typeof(Vector2))
                {
                    column = GetVector2Column(field);
                }
                else if (field.FieldType == typeof(Vector3))
                {
                    column = GetVector3Column(field);
                }
                else if (field.FieldType == typeof(Vector4))
                {
                    column = GetVector4Column(field);
                }
                else if (field.FieldType == typeof(Color))
                {
                    column = GetColorColumn(field);
                }
                else if (field.FieldType == typeof(Color32))
                {
                    column = GetColor32Column(field);
                }
                else if (field.FieldType == typeof(Rect))
                {
                    column = GetRectColumn(field);
                    rowHeightMul = 2;
                }
                else if (field.FieldType == typeof(Bounds))
                {
                    column = GetBoundsColumn(field);
                    rowHeightMul = 2;
                }
                else if (field.FieldType == typeof(AnimationCurve))
                {
                    column = GetCurveColumn(field);
                }
                else if (field.FieldType == typeof(Gradient))
                {
                    column = GetGradientColumn(field);
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    column = GetListColumn(field);
                }
                else if (field.FieldType.IsSubclassOf(typeof(UObject)))
                {
                    column = GetObjectColumn(field);
                }
                else if (field.FieldType.IsValueType && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                {
                    column = GetStructColumn(field);
                }
                if (column != null)
                {
                    column.autoResize = false;
                    column.headerContent = new GUIContent(field.Name);
                    columns.Add(column);
                }
            }
            return columns;
        }
        private TableColumn<object> GetEnumColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                Enum value = EditorGUI.EnumPopup(rect, (Enum)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetStringColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = true;
            column.Compare = (a, b) =>
            {
                string x = (string)field.GetValue(a);
                string y = (string)field.GetValue(b);
                return x.CompareTo(y);
            };
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                string value = EditorGUI.TextField(rect, (string)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetIntColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = true;
            column.Compare = (a, b) =>
            {
                int x = (int)field.GetValue(a);
                int y = (int)field.GetValue(b);
                return x.CompareTo(y);
            };
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                var obj = field.GetValue(data);
                int value = EditorGUI.IntField(rect, (int)obj);
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetLongColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = true;
            column.Compare = (a, b) =>
            {
                long x = (long)field.GetValue(a);
                long y = (long)field.GetValue(b);
                return x.CompareTo(y);
            };
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                var obj = field.GetValue(data);
                long value = EditorGUI.LongField(rect, (long)obj);
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetFloatColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = true;
            column.Compare = (a, b) =>
            {
                float x = (float)field.GetValue(a);
                float y = (float)field.GetValue(b);
                return x.CompareTo(y);
            };
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                float value = EditorGUI.FloatField(rect, (float)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetDoubleColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = true;
            column.Compare = (a, b) =>
            {
                double x = (double)field.GetValue(a);
                double y = (double)field.GetValue(b);
                return x.CompareTo(y);
            };
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                double value = EditorGUI.DoubleField(rect, (double)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetBoolColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 40;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                bool value = EditorGUI.Toggle(rect, (bool)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetVector2Column(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                Vector2 value = EditorGUI.Vector2Field(rect, "", (Vector2)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetVector3Column(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 150;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                Vector3 value = EditorGUI.Vector3Field(rect, "", (Vector3)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetVector4Column(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 200;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                Vector4 value = EditorGUI.Vector4Field(rect, "", (Vector4)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetColorColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                Color value = EditorGUI.ColorField(rect, (Color)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetColor32Column(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 100;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                Color32 value = EditorGUI.ColorField(rect, (Color32)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetRectColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 150;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                rect.y -= 10f;  //这个字段默认居中会超过行高,向上移动一点
                Rect value = EditorGUI.RectField(rect, (Rect)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetBoundsColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 250;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                rect.y -= 10f;  //这个字段默认居中会超过行高,向上移动一点
                Bounds value = EditorGUI.BoundsField(rect, (Bounds)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetObjectColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 150;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                UObject value = EditorGUI.ObjectField(rect, field.GetValue(data) as UObject, field.FieldType, true);
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetCurveColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 150;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                AnimationCurve value = EditorGUI.CurveField(rect, (AnimationCurve)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetGradientColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 150;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                Gradient value = EditorGUI.GradientField(rect, (Gradient)field.GetValue(data));
                if (EditorGUI.EndChangeCheck())
                {
                    field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetListColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 150;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();
                string displayLable = string.Empty;
                var res = field.GetValue(data);
                if (res != null && res is IList list)
                {
                    displayLable += "{";
                    for (int i = 0; i < list.Count; i++)
                    {
                        displayLable += "{";
                        displayLable += list[i].ToString();
                        displayLable += "}";
                        if (i != list.Count - 1) displayLable += ",";
                    }
                    displayLable += "}";
                }
                EditorGUI.LabelField(rect, displayLable);
                if (EditorGUI.EndChangeCheck())
                {
                    //field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }
        private TableColumn<object> GetStructColumn(FieldInfo field)
        {
            TableColumn<object> column = new TableColumn<object>();
            column.width = 150;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.BeginChangeCheck();

                FieldInfo[] structFields = field.FieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string displayLable = "{";
                for (int i = 0; i < structFields.Length; i++)
                {
                    displayLable += $"\"{structFields[i].Name}\" : {structFields[i].GetValue(field.GetValue(data))}";
                    if (i < structFields.Length - 1) displayLable += ",";
                }
                displayLable += "}";
                EditorGUI.LabelField(rect, displayLable);

                if (EditorGUI.EndChangeCheck())
                {
                    //field.SetValue(data, value);
                    if (_isAutoSave) SaveTableData();
                }
            };
            return column;
        }

        /// <summary>
        /// 保存表格数据
        /// </summary>
        public void SaveTableData()
        {
            FieldInfo fieldInfo = _target.GetType().GetField(SimpleTableConst.tableListFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null) throw new ArgumentException($"没找到{SimpleTableConst.tableListFieldName}字段");

            Type targetType = fieldInfo.FieldType;

            // 处理目标类型为 List<T> 的情况
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = targetType.GetGenericArguments()[0];
                IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

                foreach (object item in _tableView._datas)
                {
                    // 动态转换元素类型
                    object convertedItem = Convert.ChangeType(item, elementType);
                    list.Add(convertedItem);
                }

                fieldInfo.SetValue(_target, list);

                Debug.Log($"{_target} 数据已保存");
            }
            // 处理目标类型为数组 T[] 的情况
            else if (targetType.IsArray)
            {
                Type elementType = targetType.GetElementType();
                Array array = Array.CreateInstance(elementType, _tableView._datas.Count);

                for (int i = 0; i < _tableView._datas.Count; i++)
                {
                    array.SetValue(Convert.ChangeType(_tableView._datas[i], elementType), i);
                }

                fieldInfo.SetValue(_target, array);

                Debug.Log($"{_target} 数据已保存");
            }
            else
            {
                throw new InvalidOperationException("Unsupported target collection type");
            }

            EditorUtility.SetDirty(this._target);
        }
    }
}