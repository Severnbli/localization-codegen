using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Severnbli.LocalizationCodegen.Editor
{
    /// <summary>
    /// Editor for <see cref="SmartParameterTypes"/>: picks names from the variables the entry uses in any locale,
    /// adds every variable at once, and shows the warnings generation would report.
    /// </summary>
    [CustomPropertyDrawer(typeof(SmartParameterTypes))]
    sealed class SmartParameterTypesDrawer : PropertyDrawer
    {
        const float TypeWidth = 90f;
        const float IconWidth = 18f;
        const float RemoveWidth = 22f;
        const float Gap = 4f;

        static class Content
        {
            public static readonly GUIContent AddParameter = new GUIContent("Add Parameter",
                "Add a row for one of the variables this entry uses.");
            public static readonly GUIContent AddAll = new GUIContent("Add All Parameters",
                "Add a row with the inferred type for every variable that has no row yet.");
            public static readonly GUIContent Remove = new GUIContent("−", "Remove this row.");
            public static readonly GUIContent SelectVariable = new GUIContent("Select variable");
            public static readonly GUIContent Header = new GUIContent("Name / Type",
                "Variables without a row keep the inferred type. Applies when Smart Parameter Types is Mapping.");
            public const string NoContext =
                "Variables can be listed only for a string table entry. Type the variable name (or 0 for {0}).";
            public const string NoVariables =
                "This entry has no smart string placeholders. Enable Smart on a locale and add placeholders first.";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            if (!property.isExpanded)
                return line;

            var variables = SmartVariableCatalog.ForProperty(property);
            var rows = Parameters(property)?.arraySize ?? 0;
            var height = line;
            if (variables == null || variables.Count == 0)
                height += HelpBoxHeight + EditorGUIUtility.standardVerticalSpacing;
            if (variables != null)
                height += line;
            height += line * (rows + 1);
            return height;
        }

        static float HelpBoxHeight => EditorGUIUtility.singleLineHeight * 2;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var parameters = Parameters(property);
            var line = EditorGUIUtility.singleLineHeight;
            var step = line + EditorGUIUtility.standardVerticalSpacing;
            var rect = new Rect(position.x, position.y, position.width, line);

            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
            if (!property.isExpanded || parameters == null)
                return;

            rect.y += step;
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;
            rect = EditorGUI.IndentedRect(rect);
            // The rows are already indented; fields would indent themselves again.
            EditorGUI.indentLevel = 0;
            try
            {
                DrawBody(rect, property, parameters, step);
            }
            finally
            {
                EditorGUI.indentLevel = indent;
            }
        }

        static void DrawBody(Rect rect, SerializedProperty property, SerializedProperty parameters, float step)
        {
            var variables = SmartVariableCatalog.ForProperty(property);
            if (variables == null || variables.Count == 0)
            {
                var box = new Rect(rect.x, rect.y, rect.width, HelpBoxHeight);
                EditorGUI.HelpBox(box, variables == null ? Content.NoContext : Content.NoVariables, MessageType.Info);
                rect.y += HelpBoxHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            var names = new List<string>();
            for (var i = 0; i < parameters.arraySize; i++)
                names.Add(parameters.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue);

            if (variables != null)
            {
                DrawButtons(rect, property, variables, names);
                rect.y += step;
            }

            EditorGUI.LabelField(rect, Content.Header, EditorStyles.miniBoldLabel);
            rect.y += step;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var remove = -1;
            for (var i = 0; i < parameters.arraySize; i++)
            {
                if (DrawRow(rect, property, parameters.GetArrayElementAtIndex(i), i, variables, names, seen))
                    remove = i;
                rect.y += step;
            }

            if (remove >= 0)
            {
                parameters.DeleteArrayElementAtIndex(remove);
                CodegenHooks.MarkDirtyExternal();
            }
        }

        /// <summary>
        /// Where a callback writes. Menu callbacks run after the drawing pass, when the <see cref="SerializedProperty"/>
        /// may no longer be valid, so they keep the serialized object and the property path instead.
        /// </summary>
        readonly struct Target
        {
            public readonly SerializedObject SerializedObject;
            public readonly string Path;

            public Target(SerializedProperty property)
            {
                SerializedObject = property.serializedObject;
                Path = property.propertyPath;
            }
        }

        static SerializedProperty Parameters(SerializedProperty property) => property.FindPropertyRelative("m_Parameters");

        static void DrawButtons(Rect rect, SerializedProperty property, List<SmartVariableInfo> variables, List<string> names)
        {
            var missing = SmartVariableCatalog.Missing(variables, names);
            var half = (rect.width - Gap) / 2;
            var addRect = new Rect(rect.x, rect.y, half, rect.height);
            var allRect = new Rect(rect.x + half + Gap, rect.y, half, rect.height);

            var target = new Target(property);
            if (EditorGUI.DropdownButton(addRect, Content.AddParameter, FocusType.Passive))
            {
                var menu = new GenericMenu();
                if (missing.Count == 0)
                    menu.AddDisabledItem(new GUIContent("Every variable has a row"));
                foreach (var variable in missing)
                {
                    var add = variable;
                    menu.AddItem(new GUIContent(add.Label), false, () => AddRows(target, new[] { add }));
                }
                menu.DropDown(addRect);
            }

            using (new EditorGUI.DisabledScope(missing.Count == 0))
            {
                // Applied after this pass, like the menu items, so the rows drawn below stay consistent.
                if (GUI.Button(allRect, Content.AddAll))
                    EditorApplication.delayCall += () => AddRows(target, missing);
            }
        }

        /// <summary>Draws one row and returns true when its remove button was clicked.</summary>
        static bool DrawRow(
            Rect rect,
            SerializedProperty property,
            SerializedProperty element,
            int index,
            List<SmartVariableInfo> variables,
            List<string> names,
            HashSet<string> seen)
        {
            var nameProperty = element.FindPropertyRelative("m_Name");
            var typeProperty = element.FindPropertyRelative("m_Type");

            var nameWidth = rect.width - TypeWidth - IconWidth - RemoveWidth - Gap * 3;
            var nameRect = new Rect(rect.x, rect.y, nameWidth, rect.height);
            var typeRect = new Rect(nameRect.xMax + Gap, rect.y, TypeWidth, rect.height);
            var iconRect = new Rect(typeRect.xMax + Gap, rect.y, IconWidth, rect.height);
            var removeRect = new Rect(iconRect.xMax + Gap, rect.y, RemoveWidth, rect.height);

            var name = nameProperty.stringValue;
            var variable = variables != null ? SmartVariableCatalog.Find(variables, name) : null;

            if (variables == null)
            {
                EditorGUI.BeginChangeCheck();
                var typed = EditorGUI.TextField(nameRect, name);
                if (EditorGUI.EndChangeCheck())
                {
                    nameProperty.stringValue = typed;
                    CodegenHooks.MarkDirtyExternal();
                }
            }
            else
            {
                var content = variable != null
                    ? new GUIContent(variable.Label)
                    : string.IsNullOrEmpty(name) ? Content.SelectVariable : new GUIContent(name);
                if (EditorGUI.DropdownButton(nameRect, content, FocusType.Keyboard))
                    ShowNameMenu(nameRect, new Target(property), index, name, variables, names);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(typeRect, typeProperty, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
                CodegenHooks.MarkDirtyExternal();

            var normalized = SmartParameterMapping.NormalizeName(name);
            var duplicate = normalized.Length > 0 && !seen.Add(normalized);
            if (variables != null)
            {
                var problem = SmartVariableCatalog.Problem(name, (SmartParameterType)typeProperty.intValue, variable, duplicate);
                if (problem != null)
                {
                    var icon = EditorGUIUtility.IconContent("console.warnicon.sml");
                    GUI.Label(iconRect, new GUIContent(icon.image, problem));
                }
            }

            return GUI.Button(removeRect, Content.Remove, EditorStyles.miniButton);
        }

        static void ShowNameMenu(
            Rect rect,
            Target target,
            int index,
            string current,
            List<SmartVariableInfo> variables,
            List<string> names)
        {
            var normalizedCurrent = SmartParameterMapping.NormalizeName(current);
            var taken = new HashSet<string>(
                names.Where((_, i) => i != index).Select(SmartParameterMapping.NormalizeName),
                StringComparer.Ordinal);

            var menu = new GenericMenu();
            foreach (var variable in variables)
            {
                var content = new GUIContent(variable.Label);
                var selected = variable.Name == normalizedCurrent;
                if (taken.Contains(variable.Name))
                {
                    menu.AddDisabledItem(content, selected);
                    continue;
                }

                var pick = variable.Name;
                menu.AddItem(content, selected, () => Modify(target, parameters =>
                {
                    if (index < parameters.arraySize)
                        parameters.GetArrayElementAtIndex(index).FindPropertyRelative("m_Name").stringValue = pick;
                }));
            }
            menu.DropDown(rect);
        }

        static void AddRows(Target target, IEnumerable<SmartVariableInfo> variables)
        {
            var toAdd = variables.ToList();
            Modify(target, parameters =>
            {
                foreach (var variable in toAdd)
                {
                    parameters.InsertArrayElementAtIndex(parameters.arraySize);
                    var element = parameters.GetArrayElementAtIndex(parameters.arraySize - 1);
                    element.FindPropertyRelative("m_Name").stringValue = variable.Name;
                    element.FindPropertyRelative("m_Type").intValue = (int)variable.InferredType;
                }
            });
        }

        /// <summary>
        /// Changes the parameter list from a menu or button callback, which can run outside the drawing pass, so the
        /// serialized object is updated and applied here (which also records undo).
        /// </summary>
        static void Modify(Target target, Action<SerializedProperty> change)
        {
            var serializedObject = target.SerializedObject;
            if (serializedObject == null || serializedObject.targetObject == null)
                return;

            var path = target.Path;
            serializedObject.Update();
            var parameters = serializedObject.FindProperty(path)?.FindPropertyRelative("m_Parameters");
            if (parameters == null)
                return;

            change(parameters);
            serializedObject.ApplyModifiedProperties();
            CodegenHooks.MarkDirtyExternal();
        }
    }
}
