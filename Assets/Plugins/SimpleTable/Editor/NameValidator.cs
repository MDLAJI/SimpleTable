using System;
using System.Collections.Generic;

namespace SimpleTable.Editor
{
    public static class NameValidator
    {
        //C#关键字
        private static readonly HashSet<string> ReservedKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        // C#关键字和特殊限制类型
        private static readonly HashSet<string> InvalidClassNames = new HashSet<string>(StringComparer.Ordinal)
        {
            // C#关键字
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while",
        
            // 上下文关键字
            "add", "alias", "ascending", "async", "await", "by", "descending", "dynamic", "equals", "from",
            "get", "global", "group", "init", "into", "join", "let", "nameof", "notnull", "on",
            "orderby", "partial", "remove", "select", "set", "unmanaged", "value", "var", "when", "where",
            "with", "yield"
        };

        /// <summary>
        /// 检查字段名是否合法
        /// </summary>
        public static bool IsValidFieldName(string name)
        {
            // 空值检查
            if (string.IsNullOrEmpty(name))
                return false;

            string identifier = name;
            bool hasAtSign = false;

            // 处理@前缀
            if (name[0] == '@')
            {
                // 单独@无效
                if (name.Length == 1)
                    return false;

                identifier = name.Substring(1);
                hasAtSign = true;
            }

            // 检查首字符
            char firstChar = identifier[0];
            if (firstChar != '_' && !char.IsLetter(firstChar))
                return false;

            // 检查后续字符
            for (int i = 1; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (c != '_' && !char.IsLetterOrDigit(c))
                    return false;
            }

            // 检查保留关键字（区分大小写）
            if (!hasAtSign && ReservedKeywords.Contains(identifier))
                return false;

            return true;
        }

        /// <summary>
        /// 检查类名是否合法
        /// </summary>
        public static bool IsValidClassName(string name)
        {
            // 空值检查
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string identifier = name;
            bool hasAtSign = false;

            // 处理@前缀
            if (name[0] == '@')
            {
                if (name.Length == 1) // 单独@无效
                    return false;

                identifier = name.Substring(1);
                hasAtSign = true;
            }

            // 检查首字符
            char firstChar = identifier[0];
            if (firstChar != '_' && !char.IsLetter(firstChar))
                return false;

            // 检查后续字符
            for (int i = 1; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (c != '_' && !char.IsLetterOrDigit(c))
                    return false;
            }

            // 检查禁止使用的名称（不区分大小写）
            if (InvalidClassNames.Contains(identifier.ToLowerInvariant()))
                return hasAtSign;

            return true;
        }
    }
}
