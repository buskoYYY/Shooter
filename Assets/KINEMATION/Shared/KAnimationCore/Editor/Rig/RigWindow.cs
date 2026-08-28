// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using System.Collections.Generic;
using KINEMATION.Shared.KAnimationCore.Runtime.Rig;
using UnityEditor;
using UnityEngine;

namespace KINEMATION.Shared.KAnimationCore.Editor.Rig
{
    public delegate void OnItemClicked(KRigElement selection);

    public class RigWindow : EditorWindow
    {
        private OnItemClicked _onClicked;
        private OnSelectionChanged _onSelectionChanged;

        private string _searchEntry = string.Empty;

        private RigTreeWidget _rigTreeWidget;
        private bool _useSelection;
        private List<(string, int)> _selectedItems;
        private KRigElement[] _hierarchy;

        public static void ShowWindow(
            KRigElement[] hierarchy,
            OnItemClicked onClicked,
            OnSelectionChanged onSelectionChanged,
            bool useSelection,
            List<int> selection = null,
            string title = "Selection")
        {
            var window = CreateInstance<RigWindow>();

            window._useSelection = useSelection;
            window._onClicked = onClicked;
            window._onSelectionChanged = onSelectionChanged;
            window._hierarchy = hierarchy;
            window.titleContent = new GUIContent(title);

            (string, int)[] items = new (string, int)[hierarchy.Length];
            for (int i = 0; i < hierarchy.Length; i++)
            {
                items[i] = (hierarchy[i].name, 0);
            }

            window._rigTreeWidget = new RigTreeWidget();

            if (window._useSelection)
            {
                window._rigTreeWidget.rigTreeView.drawToggleBoxes = true;
                window._rigTreeWidget.rigTreeView.onSelectionChanged = window.OnSelectionChanged;
                window._selectedItems = new List<(string, int)>();
            }
            else
            {
                window._rigTreeWidget.rigTreeView.onItemClicked = window.OnTreeItemClicked;
            }

            window._rigTreeWidget.Refresh(ref items);

            if (window._useSelection && selection != null)
            {
                window._rigTreeWidget.rigTreeView.SetSelection(selection);
            }

            window.minSize = new Vector2(450f, 550f);
            window.ShowAuxWindow();
        }

        private void OnTreeItemClicked(string itemName, int index)
        {
            KRigElement selection = index >= 0 && index < _hierarchy.Length
                ? _hierarchy[index]
                : new KRigElement(index, itemName);

            _onClicked?.Invoke(selection);
            Close();
        }

        private void OnSelectionChanged(List<(string, int)> selectedItems)
        {
            _selectedItems = selectedItems;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
            _searchEntry = EditorGUILayout.TextField(_searchEntry, EditorStyles.toolbarSearchField);
            EditorGUILayout.EndHorizontal();

            _rigTreeWidget.rigTreeView.Filter(_searchEntry);
            _rigTreeWidget.Render();
        }

        private void OnDisable()
        {
            if (_useSelection)
            {
                _onSelectionChanged?.Invoke(_selectedItems);
            }
        }
    }
}
