using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SimpleTable.Editor
{
    /// <summary>
    /// 行数据编辑器
    /// </summary>
    public class RowDataEditor : EditorWindow
    {
        private Vector2 scrollPosition;

        private static FieldInfo[] fieldInfos;
        private static SerializedProperty selectedItem;
        private static SerializedObject serializedObject;
        public static void OpenWindow(Object target, int index)
        {
            var window = GetWindow<RowDataEditor>();
            window.titleContent.text = "行数据编辑器";
            window.Show();

            var fieldInfo = target.GetType().GetField(SimpleTableConst.tableListFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldValue = fieldInfo.GetValue(target);
            if (fieldValue != null && fieldValue is IList list)
            {
                fieldInfos = list[index].GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            serializedObject = new SerializedObject(target);
            var tableListProperty = serializedObject.FindProperty(SimpleTableConst.tableListFieldName);
            selectedItem = tableListProperty.GetArrayElementAtIndex(index);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            serializedObject.Update();
            if (selectedItem != null)
            {
                foreach (FieldInfo field in fieldInfos)
                {
                    SerializedProperty property = selectedItem.FindPropertyRelative(field.Name);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, new GUIContent(ObjectNames.NicifyVariableName(field.Name)), true);
                        EditorGUILayout.Space();
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void Update()
        {
            if (EditorApplication.isCompiling)
            {
                Close();
            }
        }
    }
}