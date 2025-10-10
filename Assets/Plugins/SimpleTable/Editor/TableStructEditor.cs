using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using SimpleTable.Runtime;

namespace SimpleTable.Editor
{
    public class TableStructEditor : EditorWindow
    {
        private class fieldData
        {
            public string name;
            public Type type;
            public bool isList;
            public string toolTip;
        }
        private List<fieldData> fieldDataList = new List<fieldData>();


        private Vector2 schemaScroll;
        private string structName;
        private List<Type> tableStructList = new List<Type>();

        private bool isShowNameSpace = false;   // 是否显示命名空间
        private bool isShowCodePreview = false; // 是否显示代码预览

        private int currentStructIndex = 0;
        private string inputName = "";
        private string outputPath = "Assets/Scripts/TableData"; //创建脚本的位置

        [MenuItem("Tools/SimpleTable/创建表格结构", priority = 1000)]
        public static void ShowWindow()
        {
            var window = GetWindow<TableStructEditor>("创建表格结构");
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
#if UNITY_2019_3_OR_NEWER
            tableStructList = TypeCache.GetTypesDerivedFrom<DataStructBase>().Where(t => !t.IsAbstract && !t.IsGenericType).ToList();
#else
            tableStructList = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly =>{return assembly.GetTypes();})
                .Where(type => type.IsClass && !type.IsAbstract && typeof(DataStructBase).IsAssignableFrom(type) && type != typeof(DataStructBase)).ToList();
#endif

            fieldDataList.Clear();

            if (currentStructIndex > 0)
            {
                Init(tableStructList[currentStructIndex - 1]);
                structName = tableStructList[currentStructIndex - 1].Name;
            }
        }

        private void OnGUI()
        {
            DrawStructEditor();
        }

        private void Init(Type type)
        {
            fieldDataList.Clear();

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var item in fields)
            {
                var newfield = new fieldData();

                newfield.name = item.Name;

                if (item.FieldType.IsGenericType && item.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var genericArgument = item.FieldType.GetGenericArguments()[0];

                    newfield.type = genericArgument;
                }
                else
                {
                    newfield.type = item.FieldType;
                }

                if (item.FieldType.IsGenericType && item.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    newfield.isList = true;
                }
                else
                {
                    newfield.isList = false;
                }
                TooltipAttribute tooltipAttr = item.GetCustomAttribute<TooltipAttribute>();

                newfield.toolTip = tooltipAttr != null ? tooltipAttr.tooltip : "";

                fieldDataList.Add(newfield);
            }
        }

        /// <summary>
        /// 绘制编辑器
        /// </summary>
        private void DrawStructEditor()
        {
            schemaScroll = EditorGUILayout.BeginScrollView(schemaScroll);

            // 选择表结构
            GUILayout.Space(5);
            int oldIndex = currentStructIndex;
            currentStructIndex = EditorGUILayout.Popup(currentStructIndex, GetDisplayStruct());
            if (currentStructIndex != oldIndex)
            {
                if (currentStructIndex > 0)
                {
                    Init(tableStructList[currentStructIndex - 1]);
                    structName = tableStructList[currentStructIndex - 1].Name;
                }
                else
                {
                    structName = "";
                    fieldDataList.Clear();
                }
            }

            if (currentStructIndex == 0)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("输出文件夹", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("路径", GUILayout.Width(50f));
                    GUI.enabled = false;
                    outputPath = GUILayout.TextField(outputPath);
                    GUI.enabled = true;
                    if (GUILayout.Button("浏览", GUILayout.Width(100f)))
                    {
                        outputPath = FileUtil.GetProjectRelativePath(EditorUtility.OpenFolderPanel("选择输出文件夹", Application.dataPath, ""));
                    }
                    if (GUILayout.Button("使用默认路径", GUILayout.Width(100f)))
                    {
                        outputPath = "Assets/Scripts/TableData";
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("数据表结构", EditorStyles.boldLabel);
            // 表结构名
            if (currentStructIndex == 0)
            {
                inputName = EditorGUILayout.TextField("新结构名", inputName);
                GUI.enabled = false;
                structName = EditorGUILayout.TextField("结构类名", string.IsNullOrEmpty(inputName) ? "" : inputName + "Struct");
                EditorGUILayout.TextField("数据表类名", string.IsNullOrEmpty(inputName) ? "" : inputName + "Table");
                EditorGUILayout.TextField("新建文件路径", string.IsNullOrEmpty(inputName) ? "" : outputPath + "/" + inputName + "Table.cs");
                GUI.enabled = true;
            }
            else
            {
                GUI.enabled = false;
                structName = EditorGUILayout.TextField("结构类名", structName);
                GUI.enabled = true;
            }


            GUILayout.Space(15);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("字段", EditorStyles.boldLabel, GUILayout.Width(100f));
            isShowNameSpace = EditorGUILayout.ToggleLeft("显示命名空间", isShowNameSpace, GUILayout.Width(100f));
            isShowCodePreview = EditorGUILayout.ToggleLeft("显示代码预览", isShowCodePreview, GUILayout.Width(100f));
            EditorGUILayout.EndHorizontal();

            // 字段列表
            for (int i = 0; i < fieldDataList.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.BeginHorizontal();
                    {

                        // 字段名
                        EditorGUILayout.LabelField("字段名", GUILayout.Width(50));
                        fieldDataList[i].name = EditorGUILayout.TextField(fieldDataList[i].name, GUILayout.Width(120));
                        DrawErrorFieldNameTip(i);

                        // 类型
                        GUILayout.Space(15);
                        EditorGUILayout.LabelField("类型", GUILayout.Width(40));
                        if (GUILayout.Button("选择", GUILayout.Width(40)))
                        {
                            int idx = i;
                            TypeSelectionPopup.ShowPopup((selectedType) =>
                            {
                                if (selectedType != null)
                                {
                                    fieldDataList[idx].type = selectedType;
                                    Repaint();
                                }
                            });
                        }
                        var displayTypeName = isShowNameSpace ? fieldDataList[i].type.FullName : fieldDataList[i].type.Name;
                        GUI.enabled = false;
                        EditorGUILayout.TextField(displayTypeName, GUILayout.Width(140));
                        GUI.enabled = true;

                        // 是否为列表
                        GUILayout.Space(15);
                        EditorGUILayout.LabelField("列表", GUILayout.Width(30));
                        fieldDataList[i].isList = EditorGUILayout.Toggle(fieldDataList[i].isList, GUILayout.Width(20));

                        // 工具提示
                        GUILayout.Space(15);
                        EditorGUILayout.LabelField("工具提示", GUILayout.Width(60));
                        fieldDataList[i].toolTip = EditorGUILayout.TextField(fieldDataList[i].toolTip, GUILayout.Width(120));

                        // 删除按钮
                        GUILayout.Space(15);
                        if (GUILayout.Button("×", GUILayout.Width(24)))
                        {
                            fieldDataList.RemoveAt(i);
                            GUIUtility.ExitGUI(); // 避免布局错误
                            return;
                        }

                        // 上移按钮
                        if (i <= 0) GUI.enabled = false;
                        if (GUILayout.Button("↑", GUILayout.Width(24)))
                        {
                            var t = fieldDataList[i - 1];
                            fieldDataList[i - 1] = fieldDataList[i];
                            fieldDataList[i] = t;

                            GUIUtility.ExitGUI(); // 避免布局错误
                            return;
                        }
                        GUI.enabled = true;

                        if (i >= fieldDataList.Count - 1) GUI.enabled = false;
                        if (GUILayout.Button("↓", GUILayout.Width(24)))
                        {
                            var t = fieldDataList[i + 1];
                            fieldDataList[i + 1] = fieldDataList[i];
                            fieldDataList[i] = t;

                            GUIUtility.ExitGUI(); // 避免布局错误
                            return;
                        }
                        GUI.enabled = true;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (isShowCodePreview && string.IsNullOrEmpty(fieldDataList[i].name) == false)
                    {
                        GUILayout.Label(GenerateFieldCode(i, false), EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndVertical();
            }

            // 添加新字段
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 新增字段", GUILayout.Height(30), GUILayout.Width(120)))
            {
                var newfield = new fieldData();
                newfield.name = "";
                newfield.type = typeof(int);
                newfield.isList = false;
                newfield.toolTip = "";
                fieldDataList.Add(newfield);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(20);

            // 应用按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("新建/修改表结构", GUILayout.Height(40), GUILayout.Width(250)))
            {
                // 已有的结构
                if (currentStructIndex > 0)
                {
                    var guids = AssetDatabase.FindAssets($"t:Script {structName.Replace("Struct", "Table")}");
                    if (guids.Length == 1)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        ModifyScript(path);
                    }
                    else
                    {
                        Debug.Log(guids.Length);
                        foreach (var item in guids)
                        {
                            Debug.Log(AssetDatabase.GUIDToAssetPath(item));
                        }
                    }
                }
                // 新建的结构
                else
                {
                    CreateScript(outputPath);
                }

                Repaint(); // 确保UI更新
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制字段名错误提示
        /// </summary>
        /// <param name="currIndex"></param>
        /// <returns></returns>
        private bool DrawErrorFieldNameTip(int currIndex)
        {
            // 检查规范
            if (!NameValidator.IsValidFieldName(fieldDataList[currIndex].name))
            {
                GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon", "字段命名错误"), GUILayout.Width(20), GUILayout.Height(20));
                return true;
            }

            // 检查同名
            for (int i = 0; i < fieldDataList.Count; i++)
            {
                if (fieldDataList[i].name == fieldDataList[currIndex].name && i != currIndex)
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("console.erroricon", "存在同名字段"), GUILayout.Width(20), GUILayout.Height(20));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 创建新脚本
        /// </summary>
        /// <param name="outputPath"></param>
        private void CreateScript(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogError($"输出路径无效");
                return;
            }

            // 检查类名是否合法
            if (!NameValidator.IsValidClassName(structName))
            {
                Debug.LogError($"类名无效:{structName}");
                return;
            }

            // 检查字段名是否合法且不重复
            for (int i = 0; i < fieldDataList.Count; i++)
            {
                if (!NameValidator.IsValidFieldName(fieldDataList[i].name))
                {
                    Debug.LogError($"存在无效字段名:{fieldDataList[i].name}");
                    return;
                }

                for (int j = i + 1; j < fieldDataList.Count; j++)
                {
                    if (fieldDataList[i].name == fieldDataList[j].name)
                    {
                        Debug.LogError($"存在同名字段:{fieldDataList[i].name}");
                        return;
                    }
                }
            }

            // 检查是否已存在同名文件
            if (File.Exists(outputPath + $"/{structName.Replace("Struct", "Table")}.cs"))
            {
                Debug.LogError($"存在同名文件:{outputPath + $"/{structName.Replace("Struct", "Table")}.cs"}");
                return;
            }

            // 创建文件夹
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

            // 生成代码
            string code = GenerateCode();
            //Debug.Log(code);

            // 创建文件并写入
            try
            {
                outputPath += $"/{structName.Replace("Struct", "Table")}.cs";
                FileStream writerStream = new FileStream(outputPath, FileMode.OpenOrCreate, FileAccess.Write);
                StreamWriter writer = new StreamWriter(writerStream, Encoding.UTF8);

                writer.Write(code);

                writer.Close();
                writerStream.Close();

                Debug.Log("创建成功：" + outputPath);

                // 刷新创建meta
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError("创建失败");
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// 修改已有的脚本
        /// </summary>
        /// <param name="path"></param>
        private void ModifyScript(string path)
        {
            // 检查类名是否合法
            if (!NameValidator.IsValidClassName(structName))
            {
                Debug.LogError($"类名无效:{structName}");
                return;
            }

            // 检查字段名是否合法且不重复
            for (int i = 0; i < fieldDataList.Count; i++)
            {
                if (!NameValidator.IsValidFieldName(fieldDataList[i].name))
                {
                    Debug.LogError($"存在无效字段名:{fieldDataList[i].name}");
                    return;
                }

                for (int j = i + 1; j < fieldDataList.Count; j++)
                {
                    if (fieldDataList[i].name == fieldDataList[j].name)
                    {
                        Debug.LogError($"存在同名字段:{fieldDataList[i].name}");
                        return;
                    }
                }
            }

            string code = GenerateCode();
            //Debug.Log(code);

            try
            {
                FileStream writerStream = new FileStream(path, FileMode.Truncate, FileAccess.Write);
                StreamWriter writer = new StreamWriter(writerStream, Encoding.UTF8);

                writer.Write(code);

                writer.Close();
                writerStream.Close();

                Debug.Log("修改成功：" + path);

                // 刷新创建meta
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError("创建失败");
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// 生成字段代码
        /// </summary>
        /// <param name="i">索引</param>
        /// <param name="indent">是否缩进</param>
        /// <returns></returns>
        string GenerateFieldCode(int i, bool indent)
        {
            string field = indent ? "        " : "";
            // 特性
            if (!string.IsNullOrEmpty(fieldDataList[i].toolTip))
            {
                field += $"[Tooltip(\"{fieldDataList[i].toolTip}\")] ";
            }
            //普通类型
            if (fieldDataList[i].isList == false)
            {
                if (fieldDataList[i].type == typeof(string))
                {
                    field += $"public string ";
                }
                else if (fieldDataList[i].type == typeof(int))
                {
                    field += $"public int ";
                }
                else if (fieldDataList[i].type == typeof(long))
                {
                    field += $"public long ";
                }
                else if (fieldDataList[i].type == typeof(float))
                {
                    field += $"public float ";
                }
                else if (fieldDataList[i].type == typeof(double))
                {
                    field += $"public double ";
                }
                else if (fieldDataList[i].type == typeof(bool))
                {
                    field += $"public bool ";
                }
                else if (fieldDataList[i].type.IsEnum)
                {
                    field += $"public {fieldDataList[i].type.FullName} ";
                }
                else if (fieldDataList[i].type.IsValueType && !fieldDataList[i].type.IsPrimitive && !fieldDataList[i].type.IsEnum)
                {
                    field += $"public {fieldDataList[i].type.FullName} ";
                }
                else
                {
                    field += $"public {fieldDataList[i].type.Name} ";
                }
                // 字段名
                if (fieldDataList[i].type == typeof(Gradient))
                {
                    field += $"{fieldDataList[i].name} = new Gradient();\n";
                }
                else
                {
                    field += $"{fieldDataList[i].name};\n";
                }
            }
            // 集合类型
            else
            {
                if (fieldDataList[i].type == typeof(string))
                {
                    field += $"public List<string> {fieldDataList[i].name} = new List<string>();\n";
                }
                else if (fieldDataList[i].type == typeof(int))
                {
                    field += $"public List<int> {fieldDataList[i].name} = new List<int>();\n";
                }
                else if (fieldDataList[i].type == typeof(long))
                {
                    field += $"public List<long> {fieldDataList[i].name} = new List<long>();\n";
                }
                else if (fieldDataList[i].type == typeof(float))
                {
                    field += $"public List<float> {fieldDataList[i].name} = new List<float>();\n";
                }
                else if (fieldDataList[i].type == typeof(double))
                {
                    field += $"public List<double> {fieldDataList[i].name} = new List<double>();\n";
                }
                else if (fieldDataList[i].type == typeof(bool))
                {
                    field += $"public List<bool> {fieldDataList[i].name} = new List<bool>();\n";
                }
                else if (fieldDataList[i].type.IsEnum)
                {
                    field += $"public List<{fieldDataList[i].type.FullName}> {fieldDataList[i].name} = new List<{fieldDataList[i].type.FullName}>();\n";
                }
                else if (fieldDataList[i].type.IsValueType && !fieldDataList[i].type.IsPrimitive && !fieldDataList[i].type.IsEnum)
                {
                    field += $"public List<{fieldDataList[i].type.FullName}> {fieldDataList[i].name} = new List<{fieldDataList[i].type.FullName}>();\n";
                }
                else
                {
                    field += $"public List<{fieldDataList[i].type.Name}> {fieldDataList[i].name} = new List<{fieldDataList[i].type.Name}>();\n";
                }
            }

            return field;
        }

        /// <summary>
        /// 生成代码
        /// </summary>
        string GenerateCode()
        {
            string code = "";
            code += "using System;\n";
            code += "using System.Collections.Generic;\n";
            code += "using UnityEngine;\n\n";
            code += "namespace SimpleTable.Runtime\n";
            code += "{\n";
            code += "    /// <summary>\n";
            code += "    /// 该类为自动生成，不应该手动修改，使用 “工具->表格->创建表格结构” 来修改\n";
            code += "    /// </summary>\n";
            code += "    [Serializable]\n";
            code += $"    public class {structName} : DataStructBase\n";
            code += "    {\n";
            for (int i = 0; i < fieldDataList.Count; i++)
            {
                code += GenerateFieldCode(i, true);
            }
            code += "    }\n\n";
            code += "    /// <summary>\n";
            code += "    /// 该类为自动生成，不应该修改\n";
            code += "    /// </summary>\n";
            code += $"    [CreateAssetMenu(fileName = \"{structName.Replace("Struct", "Table")}\", menuName = \"自定义/数据表/{structName.Replace("Struct", "Table")}\")]\n";
            code += $"    public class {structName.Replace("Struct", "Table")} : DataTableBase\n";
            code += "    {\n";
            code += $"        public List<{structName}> Data => tableList;\n";
            code += $"        [SerializeField] private List<{structName}> tableList = new List<{structName}>();\n\n";
            code += $"        public Type tableStruct = typeof({structName});\n\n";
            code += $"        public override void Add()\n";
            code += "        {\n";
            code += $"            tableList.Add(new {structName}());\n";
            code += "        }\n";
            code += "    }\n";
            code += "}\n";

            return code;
        }

        /// <summary>
        /// 获取表格结构类名字
        /// </summary>
        private string[] GetDisplayStruct()
        {
            List<string> displayName = new List<string>();
            displayName.Add("New...");
            foreach (var item in tableStructList)
            {
                displayName.Add(item.Name);
            }
            return displayName.ToArray();
        }
    }
}
