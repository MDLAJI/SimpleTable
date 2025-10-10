using OfficeOpenXml.Style;
using OfficeOpenXml;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Text;

namespace SimpleTable.Editor
{
    /// <summary>
    /// 数据表的导出工具
    /// </summary>
    public static class DataTableExporter
    {
        public enum ExportType { Excel, Lua }

        /// <summary>
        /// 导出为Excel表格
        /// </summary>
        public static void ExportAsExcel(UnityEngine.Object dataTableSO)
        {
            var outputPath = EditorUtility.OpenFolderPanel("导出数据表为excel", Application.dataPath, "");
            if (outputPath != string.Empty)
            {
                // 序列化表格
                var (tableTypes, tableTips, tableVariableNames, tableDatas) = SerializeDataTable(dataTableSO, ExportType.Excel);

                // 创建并写入excel
                outputPath += $"/{dataTableSO.name}.xlsx";
                FileInfo fileInfo = new FileInfo(outputPath);
                using (ExcelPackage package = new ExcelPackage(fileInfo))
                {
                    ExcelWorksheet sheet = null;
                    //创建sheet
                    if (package.Workbook.Worksheets["sheet1"] == null)
                    {
                        sheet = package.Workbook.Worksheets.Add("sheet1");
                    }
                    else
                    {
                        sheet = package.Workbook.Worksheets["sheet1"];
                        sheet.Cells.Clear();
                    }

                    //设置基本样式
                    sheet.Cells.Style.Font.Name = "微软雅黑";
                    sheet.Cells.Style.Font.Size = 10f;
                    sheet.Cells.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    sheet.Cells.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                    //写入表的基本信息
                    FieldInfo f = dataTableSO.GetType().GetField(SimpleTableConst.tableStructFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Type dataStruct = f.GetValue(dataTableSO) as Type;
                    sheet.Cells[1, 1].Value = dataTableSO.GetType().ToString();
                    sheet.Cells[1, 2].Value = dataStruct.ToString();
                    sheet.Cells[1, 3].Value = AssetDatabase.GetAssetPath(dataTableSO);
                    sheet.Cells[1, 1].Style.Font.Size = 12f;
                    sheet.Cells[1, 2].Style.Font.Size = 12f;
                    sheet.Cells[1, 3].Style.Font.Size = 12f;

                    sheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(255, 0, 255, 0);
                    sheet.Cells[1, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[1, 2].Style.Fill.BackgroundColor.SetColor(255, 0, 255, 0);
                    sheet.Cells[1, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[1, 3].Style.Fill.BackgroundColor.SetColor(255, 0, 255, 0);

                    sheet.Cells[1, 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    sheet.Cells[1, 2].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    sheet.Cells[1, 3].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                    //写入表头
                    for (int i = 0; i < tableTypes.Count; i++)
                    {
                        sheet.Cells[2, i + 1].Value = tableTypes[i];
                        sheet.Cells[3, i + 1].Value = tableTips[i];
                        sheet.Cells[4, i + 1].Value = tableVariableNames[i];

                        // 设置列宽
                        int maxCharCount = Math.Max(tableTypes[i].Length, tableTips[i].Length);
                        maxCharCount = Math.Max(tableVariableNames[i].Length, maxCharCount);
                        sheet.Column(i + 1).Width = maxCharCount * 2f;
                        sheet.Column(i + 1).Style.WrapText = true;

                        //设置区域背景颜色（设置背景颜色之前一定要设置PatternType）
                        sheet.Cells[2, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[2, i + 1].Style.Fill.BackgroundColor.SetColor(255, 255, 0, 0);
                        sheet.Cells[3, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[3, i + 1].Style.Fill.BackgroundColor.SetColor(255, 102, 204, 255);
                        sheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(255, 255, 242, 102);

                        // 描边
                        sheet.Cells[2, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        sheet.Cells[3, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                        sheet.Cells[4, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);

                        // 字体
                        sheet.Cells[2, i + 1].Style.Font.Size = 12f;
                        sheet.Cells[3, i + 1].Style.Font.Size = 12f;
                        sheet.Cells[4, i + 1].Style.Font.Size = 12f;
                    }

                    // 设置行高
                    sheet.Row(1).Height = 20;
                    sheet.Row(2).Height = 20;
                    sheet.Row(3).Height = 20;
                    sheet.Row(4).Height = 20;

                    // 冻结前四行
                    sheet.View.FreezePanes(5, 1);

                    // 写入数据
                    for (int i = 0; i < tableDatas.Count; i++)
                    {
                        for (int j = 0; j < tableDatas[i].Count; j++)
                        {
                            sheet.Cells[i + 5, j + 1].Value = tableDatas[i][j];
                        }
                    }

                    //保存
                    package.Save();

                    Debug.Log("表格已导出为Excel:" + outputPath);
                }
            }
            else
            {
                Debug.Log("取消导出表格");
            }
        }

        /// <summary>
        /// 导出为Lua Table
        /// </summary>
        /// <param name="dataTableSO"></param>
        public static void ExportAsLua(UnityEngine.Object dataTableSO)
        {
            var outputPath = EditorUtility.OpenFolderPanel("导出数据表为Lua", Application.dataPath, "");
            if (outputPath != string.Empty)
            {
                var luaTableName = dataTableSO.GetType().Name;
                outputPath += $"/{luaTableName}.lua";

                // 序列化表格数据
                var (tableTypes, tableTips, tableVariableNames, tableDatas) = SerializeDataTable(dataTableSO, ExportType.Lua);

                // 写注释
                string result = $"--- {dataTableSO.name}\n";
                for (int i = 0; i < tableTypes.Count; i++)
                {
                    result += $"---@param {tableVariableNames[i]} {tableTypes[i]} {tableTips[i]}\n";
                }

                // 用SO的类名作为LuaTable的名字
                result += $"{luaTableName}=\n{{\n";

                for (int i = 0; i < tableDatas.Count; i++)
                {
                    string luaCode = $"[{i}] =\n{{\n";
                    for (int j = 0; j < tableDatas[i].Count; j++)
                    {
                        luaCode += tableDatas[i][j];
                    }
                    luaCode += "},\n";
                    result += luaCode;
                }

                result += "}";

                // 格式化代码
                int indentLevel = 0;
                var rowArr = result.Split('\n');
                string formatCode = string.Empty;
                for (int i = 0; i < rowArr.Length; i++)
                {
                    if (rowArr[i].StartsWith("{"))
                    {
                        rowArr[i] = rowArr[i].PadLeft(rowArr[i].Length + indentLevel, '\t');
                        indentLevel++;
                    }
                    else if (rowArr[i].StartsWith("}"))
                    {
                        indentLevel--;
                        rowArr[i] = rowArr[i].PadLeft(rowArr[i].Length + indentLevel, '\t');
                    }
                    else
                    {
                        rowArr[i] = rowArr[i].PadLeft(rowArr[i].Length + indentLevel, '\t');
                    }
                    formatCode += rowArr[i] + "\n";
                }

                // 写入lua文件
                try
                {
                    FileStream writerStream;
                    if (File.Exists(outputPath))
                        writerStream = new FileStream(outputPath, FileMode.Truncate, FileAccess.Write);
                    else
                        writerStream = new FileStream(outputPath, FileMode.OpenOrCreate, FileAccess.Write);

                    StreamWriter writer = new StreamWriter(writerStream, Encoding.UTF8);

                    writer.Write(formatCode);

                    writer.Close();
                    writerStream.Close();

                    Debug.Log("导出Lua成功：" + outputPath);

                    AssetDatabase.Refresh();
                }
                catch (Exception e)
                {
                    Debug.LogError("导出Lua失败:" + e);
                }
            }
        }

        /// <summary>
        /// 序列化表格数据
        /// </summary>
        /// <param name="dataTableSO"></param>
        public static (List<string>, List<string>, List<string>, List<List<string>>) SerializeDataTable(UnityEngine.Object dataTableSO, ExportType exportType)
        {
            List<string> tableTypes = new List<string>();
            List<string> tableTips = new List<string>();
            List<string> tableVariableNames = new List<string>();
            List<List<string>> tableDatas = new List<List<string>>();

            var tableListField = dataTableSO.GetType().GetField(SimpleTableConst.tableListFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var tableListValue = tableListField.GetValue(dataTableSO);
            if (tableListValue != null && tableListValue is IList list)
            {
                // 保存类型和提示
                var fields = list[0]?.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    // 类型
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        string typeName = $"List<{field.FieldType.GetGenericArguments()[0].FullName}>";
                        tableTypes.Add(typeName);
                    }
                    else if (field.FieldType.IsEnum)
                    {
                        tableTypes.Add("(Enum)" + field.FieldType.FullName);
                    }
                    else
                    {
                        tableTypes.Add(field.FieldType.FullName);
                    }

                    // 提示
                    TooltipAttribute tooltipAttr = field.GetCustomAttribute<TooltipAttribute>();
                    tableTips.Add(tooltipAttr != null ? tooltipAttr.tooltip : "");

                    //变量名
                    tableVariableNames.Add(field.Name);
                }

                // 保存值
                for (int i = 0; i < list.Count; i++)
                {
                    List<string> newData = new List<string>();

                    var fieldInfos = list[i].GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var field in fieldInfos)
                    {
                        string result = null;
                        if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            if (exportType == ExportType.Lua)
                                result = $"{field.Name} = " + SerializeList(field, list[i], exportType) + ",\n";
                            else
                                result = SerializeList(field, list[i], exportType);
                        }
                        else
                        {
                            if (exportType == ExportType.Lua)
                                result = $"{field.Name} = " + SerializeField(field.FieldType, field.GetValue(list[i]), exportType) + ",\n";
                            else
                                result = SerializeField(field.FieldType, field.GetValue(list[i]), exportType);
                        }

                        newData.Add(result);
                    }

                    tableDatas.Add(newData);
                }
            }

            return (tableTypes, tableTips, tableVariableNames, tableDatas);
        }

        /// <summary>
        /// 序列化列表字段
        /// </summary>
        public static string SerializeList(FieldInfo field, object obj, ExportType exportType)
        {
            string result = string.Empty;
            var data = field.GetValue(obj);
            if (data != null && data is IList list)
            {
                if (exportType == ExportType.Lua)
                {
                    result += "\n{\n";
                    for (int i = 0; i < list.Count; i++)
                    {
                        result += $"[{i}] = ";
                        result += SerializeField(list[i].GetType(), list[i], exportType);
                        if (i != list.Count - 1) result += ",\n";
                    }
                    result += "\n}";
                }
                else
                {
                    result += "{";
                    for (int i = 0; i < list.Count; i++)
                    {
                        result += "{";
                        result += SerializeField(list[i].GetType(), list[i], exportType);
                        result += "}";
                        if (i != list.Count - 1) result += ",";
                    }
                    result += "}";
                }


            }
            return result;
        }

        /// <summary>
        /// 序列化对象
        /// </summary>
        public static string SerializeObject(UnityEngine.Object uobject)
        {
            string path = null;
            if (uobject == null)
            {
                return "null";
            }
            else if (EditorUtility.IsPersistent(uobject))
            {
                path = AssetDatabase.GetAssetPath(uobject);
            }
            else
            {
                Debug.LogError("只能序列化Asset中的对象");
            }
            return path;
        }

        /// <summary>
        /// 序列化AnimationCurve
        /// </summary>
        /// <param name="curve"></param>
        public static string SerializeAnimationCurve(AnimationCurve curve)
        {
            string result = string.Empty;
            if (curve == null) return result;

            result += "{";

            // 保存两个枚举值
            result += $"\"preWrapMode\" : \"{curve.preWrapMode}\", ";
            result += $"\"postWrapMode\" : \"{curve.postWrapMode}\", ";

            // 保存每个关键帧的值
            result += "\"keyframe\" : [";
            for (int i = 0; i < curve.keys.Length; i++)
            {
                result += "{";

                result += $"\"time\":{curve.keys[i].time},";
                result += $"\"value\":{curve.keys[i].value},";
                result += $"\"inTangent\":{curve.keys[i].inTangent},";
                result += $"\"outTangent\":{curve.keys[i].outTangent},";
                result += $"\"inWeight\":{curve.keys[i].inWeight},";
                result += $"\"outWeight\":{curve.keys[i].outWeight}";

                result += "}";
                if (i != curve.keys.Length - 1) result += ",";
            }
            result += "]";

            result += "}";

            return result;
        }

        /// <summary>
        /// 序列化Gradient
        /// </summary>
        public static string SerializeGradient(Gradient gradient)
        {
            string result = string.Empty;
            result += "{";

            result += $"\"mode\" : \"{gradient.mode.ToString()}\",";

            result += "\"colorKeys\":[";
            for (int i = 0; i < gradient.colorKeys.Length; i++)
            {
                result += "{";
                result += $"\"r\":{gradient.colorKeys[i].color.r}, \"g\":{gradient.colorKeys[i].color.g}, \"b\":{gradient.colorKeys[i].color.b}, \"a\":{gradient.colorKeys[i].color.a}, ";
                result += $"\"time\":{gradient.colorKeys[i].time}";
                result += "}";
                if (i == 0) result += ",";
            }
            result += "],";

            result += "\"alphaKeys\":[";
            for (int i = 0; i < gradient.alphaKeys.Length; i++)
            {
                result += "{";
                result += $"\"alpha\":{gradient.alphaKeys[i].alpha}, ";
                result += $"\"time\":{gradient.alphaKeys[i].time}";
                result += "}";
                if (i == 0) result += ",";
            }
            result += "]";

            result += "}";

            return result;
        }

        /// <summary>
        /// 序列化结构体
        /// </summary>
        public static string SerializeStruct(Type structType, object obj, ExportType exportType)
        {
            FieldInfo[] structFields = structType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            string result = "{";
            for (int i = 0; i < structFields.Length; i++)
            {
                // 序列化结构体的各个字段
                if (exportType == ExportType.Lua)
                    result += $"{structFields[i].Name} = {SerializeField(structFields[i].FieldType, structFields[i].GetValue(obj), ExportType.Lua)}";
                else
                    result += $"\"{structFields[i].Name}\" : {SerializeField(structFields[i].FieldType, structFields[i].GetValue(obj), ExportType.Excel)}";

                if (i < structFields.Length - 1) result += ", ";
            }
            result += "}";

            return result;
        }

        /// <summary>
        /// 序列化字段
        /// </summary>
        public static string SerializeField(Type fieldType, object fieldValue, ExportType exportType)
        {
            if (fieldType.IsEnum)
            {
                return ((Enum)fieldValue).ToString();
            }
            else if (fieldType == typeof(string))
            {
                return $"\"{(string)fieldValue}\"";
            }
            else if (fieldType == typeof(int))
            {
                return ((int)fieldValue).ToString();
            }
            else if (fieldType == typeof(long))
            {
                return ((long)fieldValue).ToString();
            }
            else if (fieldType == typeof(float))
            {
                return ((float)fieldValue).ToString();
            }
            else if (fieldType == typeof(double))
            {
                return ((double)fieldValue).ToString();
            }
            else if (fieldType == typeof(bool))
            {
                if (exportType == ExportType.Lua)
                    return ((bool)fieldValue).ToString().ToLower();
                else
                    return ((bool)fieldValue).ToString();
            }
            else if (fieldType == typeof(Vector2))
            {
                if (exportType == ExportType.Lua)
                    return $"{{x={((Vector2)fieldValue).x},y={((Vector2)fieldValue).y}}}";
                else
                    return ((Vector2)fieldValue).ToString();
            }
            else if (fieldType == typeof(Vector3))
            {
                if (exportType == ExportType.Lua)
                    return $"{{x={((Vector3)fieldValue).x},y={((Vector3)fieldValue).y},z={((Vector3)fieldValue).z}}}";
                else
                    return ((Vector3)fieldValue).ToString();
            }
            else if (fieldType == typeof(Vector4))
            {
                if (exportType == ExportType.Lua)
                    return $"{{x={((Vector4)fieldValue).x},y={((Vector4)fieldValue).y},z={((Vector4)fieldValue).z},w={((Vector4)fieldValue).w}}}";
                else
                    return ((Vector4)fieldValue).ToString();
            }
            else if (fieldType == typeof(Color))
            {
                if (exportType == ExportType.Lua)
                {
                    var col = (Color)fieldValue;
                    return $"{{r={col.r}, g={col.g}, b={col.b}, a={col.a}}}";
                }
                else
                    return ((Color)fieldValue).ToString();
            }
            else if (fieldType == typeof(Color32))
            {
                if (exportType == ExportType.Lua)
                {
                    var col = (Color32)fieldValue;
                    return $"{{r={col.r}, g={col.g}, b={col.b}, a={col.a}}}";
                }
                else
                    return ((Color32)fieldValue).ToString();
            }
            else if (fieldType == typeof(Rect))
            {
                if (exportType == ExportType.Lua)
                {
                    var rect = (Rect)fieldValue;
                    return $"{{x={rect.x}, y={rect.y}, width={rect.width}, height={rect.height}}}";
                }
                else
                    return ((Rect)fieldValue).ToString();
            }
            else if (fieldType == typeof(Bounds))
            {
                if (exportType == ExportType.Lua)
                {
                    var bounds = (Bounds)fieldValue;
                    return $"{{Center={{x={bounds.center.x}, y={bounds.center.y}, z={bounds.center.z}}}, Extents={{x={bounds.extents.x}, y={bounds.extents.y}, z={bounds.extents.z}}}}}";
                }
                else
                    return ((Bounds)fieldValue).ToString();
            }
            else if (fieldType == typeof(AnimationCurve))
            {
                var curve = (AnimationCurve)fieldValue;
                return SerializeAnimationCurve(curve);
            }
            else if (fieldType == typeof(Gradient))
            {
                var gradient = (Gradient)fieldValue;
                return SerializeGradient(gradient);
            }
            else if (fieldType.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                var asset = fieldValue as UnityEngine.Object;

                if (exportType == ExportType.Lua)
                    return $"\"{SerializeObject(asset)}\"";
                else
                    return SerializeObject(asset);
            }
            else if (fieldType.IsValueType && !fieldType.IsPrimitive && !fieldType.IsEnum)
            {
                return SerializeStruct(fieldType, fieldValue, exportType);
            }
            else
            {
                return null;
            }
        }
    }
}
