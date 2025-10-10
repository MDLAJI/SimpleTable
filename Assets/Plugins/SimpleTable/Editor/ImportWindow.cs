using OfficeOpenXml;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleTable.Editor
{
    public class ImportWindow : EditorWindow
    {
        public enum ImportType { Excel, Json, Lua }

        private ImportType importType;

        private Vector2 scrollPos;

        private string importPath = string.Empty;
        private Runtime.DataTableBase targetTable = null;
        private string targetTableClass = string.Empty;
        private string targetTablePath = string.Empty;

        [MenuItem("Tools/SimpleTable/导入数据表", priority = 1001)]
        public static void ShowWindow()
        {
            var window = GetWindow<ImportWindow>("导入数据表");
            window.minSize = new Vector2(400, 400);
        }

        private void OnGUI()
        {
            DrawWindow();
        }

        private void DrawWindow()
        {
            EditorGUILayout.Space();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("路径: ", GUILayout.Width(30f));
                GUI.enabled = false;
                importPath = GUILayout.TextField(importPath);
                GUI.enabled = true;
                if (GUILayout.Button("浏览", GUILayout.Width(50f)))
                {
                    importPath = EditorUtility.OpenFilePanelWithFilters("导入数据表", Application.dataPath, new string[] { "Excel表格", "xls,xlsx", "Lua文件", "lua", "Json文件", "json" });
                    var extension = Path.GetExtension(importPath).ToLower();
                    if (extension == ".xls" || extension == ".xlsx")
                    {
                        importType = ImportType.Excel;
                    }
                    else if (extension == ".lua")
                    {
                        importType = ImportType.Lua;
                    }
                    else if (extension == ".json")
                    {
                        importType = ImportType.Json;
                    }

                    CheckBeforeImport();
                }
            }
            GUILayout.EndHorizontal();

            // 显示导入方式
            if (importPath != string.Empty)
            {
                GUILayout.Label($"导入方式 : {importType}");
            }

            // 显示要覆盖的表格
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (importPath != string.Empty && targetTable != null)
            {
                GUILayout.Label("目标表格: ", GUILayout.Width(30f));
                GUI.enabled = false;
                EditorGUILayout.ObjectField(targetTable, typeof(Runtime.DataTableBase), false);
                GUI.enabled = true;
            }
            else if (importPath != string.Empty && targetTable == null)
            {
                GUILayout.Label($"未找到目标表格,\n将使用 {targetTableClass} 创建新表格,\n路径 : {targetTablePath}");
            }
            GUILayout.EndHorizontal();

            // 导入按钮
            EditorGUILayout.Space();
            GUI.enabled = importPath != string.Empty;
            if (GUILayout.Button("导入", GUILayout.Height(30)))
            {
                switch (importType)
                {
                    case ImportType.Excel:
                        DataTableImporter.ImportFromExcel(importPath);
                        break;
                    case ImportType.Json:
                        // TODO
                        break;
                    case ImportType.Lua:
                        // TODO
                        break;
                }

                importPath = string.Empty;
                targetTable = null;
                targetTableClass = string.Empty;
                targetTablePath = string.Empty;
            }
            GUI.enabled = true;

            EditorGUILayout.EndScrollView();
        }

        private void CheckBeforeImport()
        {
            if (importPath != string.Empty)
            {
                // 加载Excel文件
                using (ExcelPackage package = new ExcelPackage(new FileInfo(importPath)))
                {
                    // 获取第一个工作表
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[1];

                    // 获取工作表的行数和列数
                    int rowCount = worksheet.Dimension.Rows;
                    int colCount = worksheet.Dimension.Columns;

                    // 第一行
                    string tableClassName = worksheet.Cells[1, 1].Value as string;
                    string tableStructName = worksheet.Cells[1, 2].Value as string;
                    string tableAssetPath = worksheet.Cells[1, 3].Value as string;

                    var tableAsset = AssetDatabase.LoadAssetAtPath<SimpleTable.Runtime.DataTableBase>(tableAssetPath);
                    if (tableAsset == null)
                    {
                        targetTable = null;
                        targetTablePath = tableAssetPath;
                        targetTableClass = tableClassName;
                    }
                    else
                    {
                        targetTable = tableAsset;
                    }
                }
            }
        }
    }
}

