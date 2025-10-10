using OfficeOpenXml;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SimpleTable.Editor
{
    /// <summary>
    /// 数据表的导入工具
    /// </summary>
    public static class DataTableImporter
    {
        public static void ImportFromExcel(string excelPath)
        {
            if (excelPath != string.Empty)
            {
                // 加载Excel文件
                using (ExcelPackage package = new ExcelPackage(new FileInfo(excelPath)))
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

                    List<string> variableNames = new List<string>();
                    // 变量名
                    for (int col = 1; col <= colCount; col++)
                    {
                        object value = worksheet.Cells[4, col].Value;
                        variableNames.Add(value as string);
                    }

                    var tableAsset = AssetDatabase.LoadAssetAtPath<SimpleTable.Runtime.DataTableBase>(tableAssetPath);
                    if (tableAsset == null)
                    {
                        Debug.Log("目标表格不存在,将创建新表格");
                        tableAsset = CreateNewTable(tableClassName, tableAssetPath);
                        if (tableAsset == null) return;
                    }

                    FieldInfo tableListInfo = tableAsset.GetType().GetField(SimpleTableConst.tableListFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var tableListValue = tableListInfo.GetValue(tableAsset);

                    if (tableListValue != null && tableListValue is IList tableList)
                    {
                        tableList.Clear();

                        for (int row = 5; row <= rowCount; row++)
                        {
                            tableAsset.Add();
                            for (int col = 1; col <= colCount; col++)
                            {
                                if (variableNames[col - 1] == null) continue;

                                var field = tableList[row - 5].GetType().GetField(variableNames[col - 1], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                object deserializeValue = null;
                                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                                {
                                    deserializeValue = DeserializeList(worksheet.Cells[row, col].Value, field.FieldType);
                                }
                                else
                                {
                                    deserializeValue = DeserializeField(worksheet.Cells[row, col].Value, field.FieldType);
                                }

                                field.SetValue(tableList[row - 5], deserializeValue);
                            }
                        }

                        EditorUtility.SetDirty(tableAsset);
                        EditorGUIUtility.PingObject(tableAsset);
                    }

                    Debug.Log("导入数据表完成");
                }
            }
        }

        /// <summary>
        /// 反序列化List
        /// </summary>
        public static object DeserializeList(object value, Type type)
        {
            var result = ParseStringWithLevel(value as string, 2);
            Type elementType = type.GetGenericArguments()[0];
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

            foreach (var str in result)
            {
                var v = DeserializeField(str, elementType);
                object convertedItem = Convert.ChangeType(v, elementType);
                list.Add(convertedItem);
            }
            return list;
        }

        /// <summary>
        /// 反序列化AnimationCurve
        /// </summary>
        public static AnimationCurve DeserializeAnimationCurve(string str)
        {
            // 提取preWrapMode
            WrapMode preWrapMode = WrapMode.ClampForever;
            string preWrapModePattern = @"""preWrapMode""\s*:\s*""([^""]*)""";
            Match preWrapModeMatch = Regex.Match(str, preWrapModePattern);
            if (preWrapModeMatch.Success)
            {
                preWrapMode = (WrapMode)Enum.Parse(typeof(WrapMode), preWrapModeMatch.Groups[1].Value);
            }

            // 提取postWrapMode
            WrapMode postWrapMode = WrapMode.ClampForever;
            string postWrapModePattern = @"""preWrapMode""\s*:\s*""([^""]*)""";
            Match postWrapModeMatch = Regex.Match(str, postWrapModePattern);
            if (postWrapModeMatch.Success)
            {
                postWrapMode = (WrapMode)Enum.Parse(typeof(WrapMode), postWrapModeMatch.Groups[1].Value);
            }

            // 提取keyframe
            Keyframe[] keyframes = new Keyframe[0];
            string arrayPattern = @"""({0})""\s*:\s*\[(.*?)\]";
            Match keyframeMatch = Regex.Match(str, string.Format(arrayPattern, "keyframe"), RegexOptions.Singleline);
            if (keyframeMatch.Success)
            {
                string content = keyframeMatch.Groups[2].Value;
                string objectPattern = @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}";
                MatchCollection objects = Regex.Matches(content, objectPattern);

                keyframes = new Keyframe[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                {
                    var paramArray = objects[i].Value.Replace(" ", "").Trim('{', '}').Split(',');
                    keyframes[i].time = float.Parse(paramArray[0].Split(':')[1]);
                    keyframes[i].value = float.Parse(paramArray[1].Split(':')[1]);
                    keyframes[i].inTangent = float.Parse(paramArray[2].Split(':')[1]);
                    keyframes[i].outTangent = float.Parse(paramArray[3].Split(':')[1]);
                    keyframes[i].inWeight = float.Parse(paramArray[4].Split(':')[1]);
                    keyframes[i].outWeight = float.Parse(paramArray[5].Split(':')[1]);
                }
            }

            AnimationCurve curve = new AnimationCurve();
            curve.preWrapMode = preWrapMode;
            curve.postWrapMode = postWrapMode;
            foreach (var keyframe in keyframes)
            {
                curve.AddKey(keyframe);
            }

            return curve;
        }

        /// <summary>
        /// 反序列化Gradient
        /// </summary>
        public static Gradient DeserializeGradient(string str)
        {
            // 提取mode
            GradientMode mode = GradientMode.Blend;
            string modePattern = @"""mode""\s*:\s*""([^""]*)""";
            Match modeMatch = Regex.Match(str, modePattern);
            if (modeMatch.Success)
            {
                mode = (GradientMode)Enum.Parse(typeof(GradientMode), modeMatch.Groups[1].Value);
            }

            string arrayPattern = @"""({0})""\s*:\s*\[(.*?)\]";
            // 提取colorKeys
            GradientColorKey[] colorKeys = new GradientColorKey[0];
            Match colorKeysContentMatch = Regex.Match(str, string.Format(arrayPattern, "colorKeys"), RegexOptions.Singleline);
            if (colorKeysContentMatch.Success)
            {
                string content = colorKeysContentMatch.Groups[2].Value;
                string objectPattern = @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}";
                MatchCollection objects = Regex.Matches(content, objectPattern);

                colorKeys = new GradientColorKey[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                {
                    var paramArray = objects[i].Value.Replace(" ", "").Trim('{', '}').Split(',');
                    var color = new Color(float.Parse(paramArray[0].Split(':')[1]), float.Parse(paramArray[1].Split(':')[1]), float.Parse(paramArray[2].Split(':')[1]), float.Parse(paramArray[3].Split(':')[1]));
                    colorKeys[i].color = color;
                    colorKeys[i].time = float.Parse(paramArray[4].Split(':')[1]);
                }
            }

            // 提取alphaKeys
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[0];
            Match alphaKeysContentMatch = Regex.Match(str, string.Format(arrayPattern, "alphaKeys"), RegexOptions.Singleline);
            if (alphaKeysContentMatch.Success)
            {
                string content = alphaKeysContentMatch.Groups[2].Value;
                string objectPattern = @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}";
                MatchCollection objects = Regex.Matches(content, objectPattern);

                alphaKeys = new GradientAlphaKey[objects.Count];
                for (int i = 0; i < objects.Count; i++)
                {
                    var paramArray = objects[i].Value.Replace(" ", "").Trim('{', '}').Split(',');
                    alphaKeys[i].alpha = float.Parse(paramArray[0].Split(':')[1]);
                    alphaKeys[i].time = float.Parse(paramArray[1].Split(':')[1]);
                }
            }

            Gradient gradient = new Gradient();
            gradient.mode = mode;
            gradient.SetKeys(colorKeys, alphaKeys);

            return gradient;
        }

        /// <summary>
        /// 反序列化结构体
        /// </summary>
        public static object DeserializeStruct(Type type, string str)
        {
            var newStructInstance = Activator.CreateInstance(type);
            var result = ParseStruct(str);
            if (result != null)
            {
                var fields = type.GetFields();
                foreach (var field in fields)
                {
                    if (result.ContainsKey(field.Name))
                    {
                        field.SetValue(newStructInstance, DeserializeField(result[field.Name], field.FieldType));
                    }
                }
            }
            return newStructInstance;
        }

        /// <summary>
        /// 反序列化字段
        /// </summary>
        public static object DeserializeField(object value, Type type)
        {
            if (type == typeof(string))
            {
                var result = value as string;
                if (result[0] == '\"') result = result.Substring(1);
                if (result[result.Length - 1] == '\"') result = result.Substring(0, result.Length - 1);
                return result;
            }
            else if (type == typeof(int))
            {
                return int.Parse(value as string);
            }
            else if (type == typeof(long))
            {
                return long.Parse(value as string);
            }
            else if (type == typeof(float))
            {
                return float.Parse(value as string);
            }
            else if (type == typeof(double))
            {
                return double.Parse(value as string);
            }
            else if (type == typeof(bool))
            {
                return bool.Parse(value as string);
            }
            else if (type == typeof(bool))
            {
                return bool.Parse(value as string);
            }
            else if (type.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                var asset = AssetDatabase.LoadAssetAtPath(value as string, type);
                return asset;
            }
            else if (type == typeof(Vector2))
            {
                var arr = (value as string).Replace("(", "").Replace(")", "").Replace(" ", "").Split(',');
                return new Vector2(float.Parse(arr[0]), float.Parse(arr[1]));
            }
            else if (type == typeof(Vector3))
            {
                var arr = (value as string).Replace("(", "").Replace(")", "").Replace(" ", "").Split(',');
                return new Vector3(float.Parse(arr[0]), float.Parse(arr[1]), float.Parse(arr[2]));
            }
            else if (type == typeof(Vector4))
            {
                var arr = (value as string).Replace("(", "").Replace(")", "").Replace(" ", "").Split(',');
                return new Vector4(float.Parse(arr[0]), float.Parse(arr[1]), float.Parse(arr[2]), float.Parse(arr[3]));
            }
            else if (type == typeof(Color))
            {
                var arr = (value as string).Replace("(", "").Replace(")", "").Replace(" ", "").Replace("RGBA", "").Split(',');
                return new Color(float.Parse(arr[0]), float.Parse(arr[1]), float.Parse(arr[2]), float.Parse(arr[3]));
            }
            else if (type == typeof(Color32))
            {
                var arr = (value as string).Replace("(", "").Replace(")", "").Replace(" ", "").Replace("RGBA", "").Split(',');
                return new Color32(byte.Parse(arr[0]), byte.Parse(arr[1]), byte.Parse(arr[2]), byte.Parse(arr[3]));
            }
            else if (type == typeof(Rect))
            {
                var arr = (value as string).Replace("(", "").Replace(")", "").Replace(" ", "").Split(',');
                return new Rect(float.Parse(arr[0].Split(':')[1]), float.Parse(arr[1].Split(':')[1]), float.Parse(arr[2].Split(':')[1]), float.Parse(arr[3].Split(':')[1]));
            }
            else if (type == typeof(Bounds))
            {
                var arr = (value as string).Replace("(", "").Replace(")", "").Replace(" ", "").Split(',');
                var center = new Vector3(float.Parse(arr[0].Split(':')[1]), float.Parse(arr[1]), float.Parse(arr[2]));
                var extents = new Vector3(float.Parse(arr[3].Split(':')[1]), float.Parse(arr[4]), float.Parse(arr[5]));
                return new Bounds(center, extents * 2);
            }
            else if (type == typeof(Gradient))
            {
                return DeserializeGradient(value as string);
            }
            else if (type == typeof(AnimationCurve))
            {
                return DeserializeAnimationCurve(value as string);
            }
            else if (type.IsEnum)
            {
                return Enum.Parse(type, value as string);
            }
            else if (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
            {
                return DeserializeStruct(type, value as string);
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// 解析列表字符串,从指定层级提取字符串
        /// </summary>
        public static List<string> ParseStringWithLevel(string input, int targetLevel)
        {
            List<string> results = new List<string>();
            Stack<int> braceStack = new Stack<int>();
            int currentLevel = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '{')
                {
                    currentLevel++;
                    braceStack.Push(i);

                    if (currentLevel == targetLevel)
                    {
                        int startIndex = i;
                    }
                }
                else if (input[i] == '}')
                {
                    if (braceStack.Count > 0)
                    {
                        int startIndex = braceStack.Pop();

                        if (currentLevel == targetLevel)
                        {
                            string content = input.Substring(startIndex + 1, i - startIndex - 1);
                            results.Add(content);
                        }

                        currentLevel--;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 解析结构体字符串
        /// </summary>
        public static Dictionary<string, string> ParseStruct(string str)
        {
            // 去掉首尾的括号
            if (str[0] == '{') str = str.Substring(1);
            if (str[str.Length - 1] == '}') str = str.Substring(0, str.Length - 1);
            var charArray = str.ToCharArray();

            Stack<char> stack = new Stack<char>();
            Stack<char> braceStack = new Stack<char>();

            int startIdx = 0;
            int endIdx = 0;

            bool isKey = true;
            List<string> keys = new List<string>();
            List<string> values = new List<string>();
            bool isStructValue = false;
            bool isValueStart = true;
            bool isValueEnd = false;

            for (int i = 0; i < charArray.Length; i++)
            {
                // 提取key
                if (isKey && charArray[i] == '\"')
                {
                    if (stack.Count == 0)
                    {
                        // stack中没引号,说明这是开始
                        stack.Push(charArray[i]);
                        startIdx = i;
                    }
                    else
                    {
                        // stack中有引号,就pop一个配对引号,然后检查栈是否为空,为空则说明结束
                        stack.Pop();
                        if (stack.Count == 0)
                        {
                            endIdx = i;
                            var res = str.Substring(startIdx + 1, endIdx - startIdx - 1).Replace("\\", "");
                            keys.Add(res);
                            isKey = !isKey;
                        }
                    }
                }
                // 提取value
                else if (!isKey)
                {
                    if (charArray[i] == ':' || charArray[i] == ' ') continue;

                    // 如果 : 后的第一个字符是 { , 说明这是一个结构体
                    if (isValueStart)
                    {
                        if (charArray[i] == '{')
                        {
                            if (braceStack.Count == 0) startIdx = i;
                            isStructValue = true;
                            isValueStart = false;
                        }
                        else
                        {
                            startIdx = i;
                            isStructValue = false;
                            isValueStart = false;
                        }
                    }

                    // 引号
                    if (charArray[i] == '\"')
                    {
                        if (stack.Count == 0) stack.Push(charArray[i]);
                        else stack.Pop();
                    }

                    // 值为结构体
                    if (isStructValue)
                    {
                        // 跳过字符串内的大括号
                        if (charArray[i] == '{' && stack.Count == 0)
                        {
                            braceStack.Push(charArray[i]);
                        }
                        else if (charArray[i] == '}' && stack.Count == 0)
                        {
                            braceStack.Pop();
                            if (braceStack.Count == 0)
                            {
                                endIdx = i + 1;
                                isValueEnd = true;
                            }
                        }
                    }
                    else   // 值为非结构体
                    {
                        if (charArray.Length - 1 == i)
                        {
                            endIdx = i + 1;
                            isValueEnd = true;
                        }
                        else if (stack.Count == 0 && (charArray[i] == ',' || charArray[i] == ' '))
                        {
                            endIdx = i;
                            isValueEnd = true;
                        }
                    }

                    if (isValueEnd)
                    {
                        isKey = !isKey;
                        isValueStart = true;
                        isValueEnd = false;
                        var res = str.Substring(startIdx, endIdx - startIdx);
                        values.Add(res);
                    }
                }
            }

            if (keys.Count != values.Count)
            {
                Debug.LogError("序列化失败,键和值的长度不一致");
                return null;
            }

            Dictionary<string, string> result = new Dictionary<string, string>();
            for (int i = 0; i < keys.Count; i++)
            {
                result.Add(keys[i], values[i]);
            }

            return result;
        }

        /// <summary>
        /// 创建新表格
        /// </summary>
        /// <param name="tableClassName">表格类名</param>
        /// <param name="tableAssetPath">表格资产路径</param>
        /// <returns></returns>
        private static Runtime.DataTableBase CreateNewTable(string tableClassName, string tableAssetPath)
        {
            // 找类
            var targetType = GetTypeInAllAssemblies(tableClassName);
            if (targetType == null)
            {
                Debug.LogError($"未找到 {tableClassName} ,创建表格失败,导入结束");
                return null;
            }

            // 创建实例
            ScriptableObject assetInstance = ScriptableObject.CreateInstance(targetType);

            // 创建文件夹
            var directoryPath = Path.GetDirectoryName(tableAssetPath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // 防止重名
            tableAssetPath = AssetDatabase.GenerateUniqueAssetPath(tableAssetPath);

            // 创建SO
            AssetDatabase.CreateAsset(assetInstance, tableAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"表格创建成功: {tableAssetPath}");

            // 创建完成后再加载一次
            var tableAsset = AssetDatabase.LoadAssetAtPath<SimpleTable.Runtime.DataTableBase>(tableAssetPath);
            return tableAsset;
        }

        /// <summary>
        /// 在所有程序集中查找数据表类
        /// </summary>
        private static Type GetTypeInAllAssemblies(string className)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(className);
                if (type != null && type.IsSubclassOf(typeof(Runtime.DataTableBase)))
                    return type;
            }
            return null;
        }
    }
}
