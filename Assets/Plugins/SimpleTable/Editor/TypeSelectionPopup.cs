using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Video;

namespace SimpleTable.Editor
{
    public class TypeSelectionPopup : EditorWindow
    {
        private static TypeSelectionPopup currentInstance;
        private Action<Type> onTypeSelected;
        private List<Type> allTypes;
        private List<Type> filteredTypes;
        private string searchText = "";
        private Vector2 scrollPosition;
        private Vector2 assemblyScrollPosition;

        // 程序集筛选相关
        private List<AssemblyInfo> allAssemblies;
        private List<string> selectedAssemblies = new List<string>();
        private bool isShowAssemblyFilter = false;

        // 颜色定义
        private static readonly Color HoverColor = new Color(0.3f, 0.6f, 1f, 0.3f);
        private static readonly Color NormalColor = new Color(0, 0, 0, 0);

        // 类型页签
        enum TypeTab
        {
            Common, Enum, Struct
        }
        private TypeTab currentTypeTab = TypeTab.Common;

        // 程序集信息类
        private class AssemblyInfo
        {
            public string Name;
            public string FullName;
            public int TypeCount;

            public AssemblyInfo(string name, string fullName, int typeCount)
            {
                Name = name;
                FullName = fullName;
                TypeCount = typeCount;
            }
        }

        public static void ShowPopup(Action<Type> callback)
        {
            // 确保这个窗口是唯一的
            // 如果已有实例，则聚焦到现有窗口而不是创建新窗口
            if (currentInstance != null)
            {
                currentInstance.Focus();
                currentInstance.onTypeSelected = callback; // 更新回调
                return;
            }

            currentInstance = CreateInstance<TypeSelectionPopup>();
            currentInstance.onTypeSelected = callback;
            currentInstance.titleContent = new GUIContent($"选择类型 (V{SimpleTableConst.version})");
            currentInstance.minSize = new Vector2(450, 650);

            // 居中显示窗口
            var mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            currentInstance.position = new Rect(mousePos.x - 225, mousePos.y - 275, 450, 550);

            currentInstance.ShowUtility();
            currentInstance.Focus();
        }

        private void OnEnable()
        {
            // 注册当前实例
            currentInstance = this;

            GetAllCommonType();
        }

        private void OnLostFocus()
        {
            Close();
        }

        private void OnDestroy()
        {
            // 如果销毁的是当前实例，则清除引用
            if (currentInstance == this)
            {
                currentInstance = null;
            }

            // 确保回调被清理
            onTypeSelected = null;
        }

        /// <summary>
        /// 获取常用类型
        /// </summary>
        private void GetAllCommonType()
        {
            allTypes = new List<Type>()
            {
                typeof(string),
                typeof(int),
                typeof(long),
                typeof(float),
                typeof(double),
                typeof(bool),
                typeof(GameObject),
                typeof(Vector2),
                typeof(Vector3),
                typeof(Vector4),
                typeof(Color),
                typeof(Color32),
                typeof(Rect),
                typeof(Bounds),
                typeof(Gradient),
                typeof(AnimationCurve),
                typeof(Sprite),
                typeof(Texture),
                typeof(Texture2D),
                typeof(AudioClip),
                typeof(AnimationClip),
                typeof(VideoClip),
                typeof(Material),
                typeof(Shader)
            };
            FilterTypes();
        }

        /// <summary>
        /// 获取结构体类型
        /// </summary>
        private void GetAllStructsAndAssemblies()
        {
            allTypes = new List<Type>();
            allAssemblies = new List<AssemblyInfo>();
            selectedAssemblies.Clear();

            // 获取所有已加载的程序集
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    // 只检查可以安全访问的程序集
                    if (assembly.IsDynamic)
                        continue;

                    var types = assembly.GetTypes();
                    int structCount = 0;

                    foreach (var type in types)
                    {
                        //只找有Serializable特性的结构体
                        if (type.IsValueType && !type.IsPrimitive && !type.IsEnum && type.IsDefined(typeof(SerializableAttribute), false))
                        {
                            allTypes.Add(type);
                            structCount++;
                        }
                    }

                    if (structCount > 0)
                    {
                        var assemblyName = assembly.GetName().Name;
                        var assemblyInfo = new AssemblyInfo(assemblyName, assembly.FullName, structCount);
                        allAssemblies.Add(assemblyInfo);

                        // 默认选择的程序集
                        if (assemblyName == "Assembly-CSharp")
                        {
                            selectedAssemblies.Add(assemblyName);
                        }
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    continue;
                }
            }

            // 按程序集名称排序
            allAssemblies = allAssemblies.OrderBy(a => a.Name).ToList();

            // 按名称排序枚举
            allTypes = allTypes.OrderBy(t => t.FullName).ToList();
            FilterTypes(); // 初始筛选
        }


        private void GetAllEnumsAndAssemblies()
        {
            allTypes = new List<Type>();
            allAssemblies = new List<AssemblyInfo>();
            selectedAssemblies.Clear();

            // 获取所有已加载的程序集
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    // 只检查可以安全访问的程序集
                    if (assembly.IsDynamic)
                        continue;

                    var types = assembly.GetTypes();
                    int enumCount = 0;

                    foreach (var type in types)
                    {
                        // 跳过非枚举类型
                        if (!type.IsEnum) continue;

                        // 跳过嵌套类型（类内部的枚举）
                        if (type.IsNested) continue;

                        // 检查访问修饰符：只保留public枚举
                        if (type.IsPublic)
                        {
                            allTypes.Add(type);
                            enumCount++;
                        }
                    }

                    if (enumCount > 0)
                    {
                        var assemblyName = assembly.GetName().Name;
                        var assemblyInfo = new AssemblyInfo(assemblyName, assembly.FullName, enumCount);
                        allAssemblies.Add(assemblyInfo);

                        // 默认选择的程序集
                        if (assemblyName == "Assembly-CSharp")
                        {
                            selectedAssemblies.Add(assemblyName);
                        }
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    continue;
                }
            }

            // 按程序集名称排序
            allAssemblies = allAssemblies.OrderBy(a => a.Name).ToList();

            // 按名称排序枚举
            allTypes = allTypes.OrderBy(t => t.FullName).ToList();
            FilterTypes();
        }

        private void OnGUI()
        {
            DrawHeader();
            if (currentTypeTab != TypeTab.Common) DrawAssemblyFilter();
            DrawSearchTool();
            DrawTypeList();
            DrawFooter();

            if (Event.current.type == EventType.Repaint)
            {
                Repaint();
            }
        }

        /// <summary>
        /// 绘制顶部
        /// </summary>
        private void DrawHeader()
        {
            // 绘制类型页签
            EditorGUILayout.BeginHorizontal();
            {
                GUI.color = currentTypeTab == TypeTab.Common ? Color.green : Color.white;
                if (GUILayout.Button("常用类型"))
                {
                    currentTypeTab = TypeTab.Common;
                    GetAllCommonType();
                }
                GUI.color = Color.white;

                GUI.color = currentTypeTab == TypeTab.Enum ? Color.green : Color.white;
                if (GUILayout.Button("枚举"))
                {
                    currentTypeTab = TypeTab.Enum;
                    GetAllEnumsAndAssemblies();
                }
                GUI.color = Color.white;

                GUI.color = currentTypeTab == TypeTab.Struct ? Color.green : Color.white;
                if (GUILayout.Button("结构体"))
                {
                    currentTypeTab = TypeTab.Struct;
                    GetAllStructsAndAssemblies();
                }
                GUI.color = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // 如果不是常用类型,要绘制程序集筛选
            if (currentTypeTab != TypeTab.Common)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("请选择一个程序集");
                EditorGUILayout.LabelField($"在 {selectedAssemblies.Count} 个程序集中找到 {filteredTypes.Count} 个类型", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// 绘制程序集筛选
        /// </summary>
        private void DrawAssemblyFilter()
        {
            EditorGUILayout.BeginHorizontal();

            // 程序集筛选器开关
            isShowAssemblyFilter = EditorGUILayout.Foldout(isShowAssemblyFilter, "程序集筛选", true);

            // 显示已选择的程序集数量
            EditorGUILayout.LabelField($"{selectedAssemblies.Count}/{allAssemblies.Count} 已选择", EditorStyles.miniLabel, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();

            if (isShowAssemblyFilter)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 全选/全不选按钮
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("选择全部", EditorStyles.miniButtonLeft))
                {
                    selectedAssemblies.Clear();
                    foreach (var assembly in allAssemblies)
                    {
                        selectedAssemblies.Add(assembly.Name);
                    }
                    FilterTypes();
                }

                if (GUILayout.Button("取消全部", EditorStyles.miniButtonMid))
                {
                    selectedAssemblies.Clear();
                    FilterTypes();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // 程序集列表
                assemblyScrollPosition = EditorGUILayout.BeginScrollView(assemblyScrollPosition, GUILayout.Height(100));

                for (int i = 0; i < allAssemblies.Count; i++)
                {
                    var assembly = allAssemblies[i];
                    bool isSelected = selectedAssemblies.Contains(assembly.Name);

                    EditorGUILayout.BeginHorizontal();

                    // 选择框
                    bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                    if (newSelected != isSelected)
                    {
                        if (newSelected)
                        {
                            selectedAssemblies.Add(assembly.Name);
                        }
                        else
                        {
                            selectedAssemblies.Remove(assembly.Name);
                        }
                        FilterTypes();
                    }

                    // 程序集名称和类型数量
                    EditorGUILayout.LabelField($"{assembly.Name} ({assembly.TypeCount} types)", EditorStyles.miniLabel);

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }
        }

        /// <summary>
        /// 绘制搜索框
        /// </summary>
        private void DrawSearchTool()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(EditorGUIUtility.IconContent("Search Icon"), GUILayout.Width(20));

            var newSearchText = EditorGUILayout.TextField(searchText);
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                FilterTypes();
            }

            // 清空搜索按钮
            if (GUILayout.Button("X", GUILayout.Width(20)) && !string.IsNullOrEmpty(searchText))
            {
                searchText = "";
                FilterTypes();
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        /// <summary>
        /// 筛选类型
        /// </summary>
        private void FilterTypes()
        {
            var result = allTypes;

            // 根据程序集筛选
            if (currentTypeTab != TypeTab.Common)
            {
                if (selectedAssemblies.Count > 0)
                    result = result.Where(t => selectedAssemblies.Contains(t.Assembly.GetName().Name)).ToList();
                else
                    result = new List<Type> { };
            }

            // 根据搜索文本筛选
            if (!string.IsNullOrEmpty(searchText))
            {
                var searchLower = searchText.ToLower();
                result = result.Where(t =>
                    t.FullName.ToLower().Contains(searchLower) ||
                    t.Name.ToLower().Contains(searchLower) ||
                    (t.Namespace != null && t.Namespace.ToLower().Contains(searchLower))
                ).ToList();
            }

            filteredTypes = result.ToList();
        }

        /// <summary>
        /// 绘制类型选项
        /// </summary>
        private void DrawTypeList()
        {
            EditorGUILayout.LabelField($"匹配的类型 : {filteredTypes.Count}");

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            if (filteredTypes.Count == 0)
            {
                if (allTypes.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前程序集中没有找到任何类型", MessageType.Info);
                }
                else if (selectedAssemblies.Count == 0)
                {
                    EditorGUILayout.HelpBox("未选择程序集,请先选择至少一个程序集", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("没有匹配的类型", MessageType.Info);
                }
            }
            else
            {
                for (int i = 0; i < filteredTypes.Count; i++)
                {
                    var type = filteredTypes[i];

                    // 开始绘制类型选项
                    Rect itemRect = EditorGUILayout.BeginVertical();
                    {
                        // 检查鼠标是否悬停在选项上
                        bool isHovered = itemRect.Contains(Event.current.mousePosition);

                        // 绘制背景色
                        Color backgroundColor = isHovered ? HoverColor : NormalColor;
                        EditorGUI.DrawRect(itemRect, backgroundColor);

                        // 鼠标点击事件
                        if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                        {
                            if (Event.current.button == 0)
                            {
                                Event.current.Use();
                                onTypeSelected?.Invoke(type);
                                Close();    // 选择选项后直接关闭这个窗口
                                return;
                            }
                        }

                        // 绘制内容
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        {
                            // 类型名称
                            EditorGUILayout.LabelField(type.Name, EditorStyles.boldLabel);

                            // 显示类型的额外信息
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUILayout.LabelField("", GUILayout.Width(20)); // 缩进

                                EditorGUILayout.BeginVertical();
                                {
                                    EditorGUILayout.LabelField($"Namespace: {type.Namespace}", EditorStyles.miniLabel);
                                    EditorGUILayout.LabelField($"Assembly: {type.Assembly.GetName().Name}", EditorStyles.miniLabel);
                                }
                                EditorGUILayout.EndVertical();
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(5f);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制底部
        /// </summary>
        private void DrawFooter()
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            // 取消按钮
            if (GUILayout.Button("取消", GUILayout.Width(80)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
