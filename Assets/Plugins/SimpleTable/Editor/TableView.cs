/*
 * 此文件基于原项目 HTFramework (https://github.com/SaiTingHu/HTFramework) 的代码修改
 * 原文件版权归原作者所有，遵循MIT许可证。
 * 
 * 原始版权声明：
 * Copyright (c) 2019 HuTao
 * 
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SimpleTable.Editor
{
    /// <summary>
    /// 表格视图
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public sealed class TableView<T> : TreeView where T : class, new()
    {
        /// <summary>
        /// 表格数据
        /// </summary>
        public List<T> _datas;
        /// <summary>
        /// 当前选择的表格数据
        /// </summary>
        private List<T> _selectionDatas;
        /// <summary>
        /// 根元素
        /// </summary>
        private TableViewItem<T> _rootItem;
        /// <summary>
        /// 所有的元素
        /// </summary>
        private List<TableViewItem<T>> _items;
        /// <summary>
        /// 所有的元素绘制项
        /// </summary>
        private List<TreeViewItem> _drawItems;
        /// <summary>
        /// 搜索控件
        /// </summary>
        private SearchField _searchField;
        /// <summary>
        /// 元素ID标记
        /// </summary>
        private int _idSign = 0;
        /// <summary>
        /// 数据表类型
        /// </summary>
        public Type tableDataStruct;
        /// <summary>
        /// 数据表对象
        /// </summary>
        public object target;

        public Action AutoSaveAction;

        private Rect realTreeViewRect; // 存储TreeView在窗口中的实际位置
        private int hoveredColumnIndex = -1;
        public string[] ColumnToolTip;

        /// <summary>
        /// 行高度
        /// </summary>
        public float RowHeight
        {
            get
            {
                return rowHeight;
            }
            set
            {
                rowHeight = value;
            }
        }
        /// <summary>
        /// 是否启用上下文右键点击
        /// </summary>
        public bool IsEnableContextClick { get; set; } = true;
        /// <summary>
        /// 是否允许多选
        /// </summary>
        public bool IsCanMultiSelect { get; set; } = true;
        /// <summary>
        /// 是否启用搜索框
        /// </summary>
        public bool IsEnableSearch { get; set; } = false;

        /// <summary>
        /// 表格视图
        /// </summary>
        /// <param name="datas">表格视图数据</param>
        /// <param name="columns">表格视图的所有列</param>
        public TableView(List<T> datas, List<TableColumn<T>> columns) : base(new TreeViewState())
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = EditorGUIUtility.singleLineHeight + 4;
            columns.Insert(0, GetIndexColumn());
            multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns.ToArray()));
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.visibleColumnsChanged += OnVisibleColumnsChanged;

            _datas = datas;
            _selectionDatas = new List<T>();
            _rootItem = new TableViewItem<T>(-1, -1, null);
            _items = new List<TableViewItem<T>>();
            for (var i = 0; i < _datas.Count; i++)
            {
                _items.Add(new TableViewItem<T>(_idSign, 0, _datas[i]));
                _idSign += 1;
            }
            _drawItems = new List<TreeViewItem>();
            _searchField = new SearchField();

            Reload();
        }

        /// <summary>
        /// 构造根节点
        /// </summary>
        protected override TreeViewItem BuildRoot()
        {
            return _rootItem;
        }

        /// <summary>
        /// 构造所有行
        /// </summary>
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            _drawItems.Clear();

            // 搜索
            if (!string.IsNullOrEmpty(searchString))
            {
                string _searchString = searchString.Replace(" ", "");

                for (int i = 0; i < _items.Count; i++)
                {
                    var fields = _items[i].Data.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    bool isSearched = false;
                    foreach (FieldInfo field in fields)
                    {
                        object value = field.GetValue(_items[i].Data);
                        //Debug.Log($"{field.Name}: {value} ({field.FieldType.Name})");
                        if (value.ToString().ToLower().Contains(_searchString.ToLower()))
                        {
                            isSearched = true;
                        }
                    }

                    if (isSearched) _drawItems.Add(_items[i]);
                }
            }
            // 不搜索
            else
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    _drawItems.Add(_items[i]);
                }
            }

            return _drawItems;
        }

        /// <summary>
        /// 绘制行
        /// </summary>
        protected override void RowGUI(RowGUIArgs args)
        {
            TableViewItem<T> item = args.item as TableViewItem<T>;
            int visibleColumns = args.GetNumVisibleColumns();
            for (var i = 0; i < visibleColumns; i++)
            {
                Rect cellRect = args.GetCellRect(i);
                int index = args.GetColumn(i);
                CenterRectUsingSingleLineHeight(ref cellRect);
                TableColumn<T> column = multiColumnHeader.GetColumn(index) as TableColumn<T>;
                column.DrawCell?.Invoke(cellRect, item.Data, args.row, args.selected, args.focused);
            }
        }

        /// <summary>
        /// 双击行
        /// </summary>
        /// <param name="id"></param>
        protected override void DoubleClickedItem(int id)
        {
            base.DoubleClickedItem(id);

            // 双击打开行编辑器
            TableViewItem<T> item = _items.Find((it) => { return it.id == id; });
            if (item != null) RowDataEditor.OpenWindow(target as UnityEngine.Object, _datas.IndexOf(item.Data));

        }

        /// <summary>
        /// 上下文右键点击
        /// </summary>
        protected override void ContextClicked()
        {
            if (!IsEnableContextClick)
                return;
            List<T> selectedItems = new List<T>();
            foreach (var itemID in GetSelection())
            {
                TableViewItem<T> item = _items.Find((it) => { return it.id == itemID; });
                if (item != null) selectedItems.Add(item.Data);
            }

            GenericMenu menu = new GenericMenu();

            if (selectedItems.Count == 1)
            {
                menu.AddItem(new GUIContent("编辑行"), false, () =>
                {
                    RowDataEditor.OpenWindow(target as UnityEngine.Object, _datas.IndexOf(selectedItems[0]));
                });
                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent("增加行"), false, () =>
            {
                object newInstance = Activator.CreateInstance(tableDataStruct);
                AddData(newInstance as T);
                AutoSaveAction?.Invoke();
            });
            if (selectedItems.Count > 0)
            {
                menu.AddItem(new GUIContent("删除所选行"), false, () =>
                {
                    DeleteDatas(selectedItems);
                    AutoSaveAction?.Invoke();
                });
            }

            if (selectedItems.Count == 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("上移所选行"), false, () =>
                {
                    var currIndex = _datas.IndexOf(selectedItems[0]);
                    if (currIndex > 0)
                    {
                        var t = _datas[currIndex - 1];
                        _datas[currIndex - 1] = _datas[currIndex];
                        _datas[currIndex] = t;

                        var t1 = _items[currIndex - 1];
                        _items[currIndex - 1] = _items[currIndex];
                        _items[currIndex] = t1;

                        Reload();
                        AutoSaveAction?.Invoke();
                    }
                });
                menu.AddItem(new GUIContent("下移所选行"), false, () =>
                {
                    var currIndex = _datas.IndexOf(selectedItems[0]);
                    if (currIndex < _datas.Count - 1)
                    {
                        var t = _datas[currIndex + 1];
                        _datas[currIndex + 1] = _datas[currIndex];
                        _datas[currIndex] = t;

                        var t1 = _items[currIndex + 1];
                        _items[currIndex + 1] = _items[currIndex];
                        _items[currIndex] = t1;

                        Reload();
                        AutoSaveAction?.Invoke();
                    }
                });
                menu.AddItem(new GUIContent("上移到顶部"), false, () =>
                {
                    var currIndex = _datas.IndexOf(selectedItems[0]);
                    if (currIndex > 0)
                    {
                        var t = _datas[0];
                        _datas[0] = _datas[currIndex];
                        _datas[currIndex] = t;

                        var t1 = _items[0];
                        _items[0] = _items[currIndex];
                        _items[currIndex] = t1;

                        Reload();
                        AutoSaveAction?.Invoke();
                    }
                });
                menu.AddItem(new GUIContent("下移到底部"), false, () =>
                {
                    var currIndex = _datas.IndexOf(selectedItems[0]);
                    if (currIndex < _datas.Count - 1)
                    {
                        var t = _datas[_datas.Count - 1];
                        _datas[_datas.Count - 1] = _datas[currIndex];
                        _datas[currIndex] = t;

                        var t1 = _items[_datas.Count - 1];
                        _items[_datas.Count - 1] = _items[currIndex];
                        _items[currIndex] = t1;

                        Reload();
                        AutoSaveAction?.Invoke();
                    }
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("清空所有行"), false, () =>
            {
                var result = EditorUtility.DisplayDialog("清空所有行", "你确定要删除表格中的所有数据吗?", "确定", "取消");
                if (result)
                {
                    ClearData();
                    AutoSaveAction?.Invoke();
                }
            });
            menu.ShowAsContext();
        }

        /// <summary>
        /// 当选择项改变
        /// </summary>
        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);

            _selectionDatas.Clear();
            foreach (var itemID in selectedIds)
            {
                TableViewItem<T> item = _items.Find((it) => { return it.id == itemID; });
                if (item != null) _selectionDatas.Add(item.Data);
            }

            // 如果行数据编辑器已打开,则在选择项改变时显示对应的数据
            var isRowDataEditorOpening = false;
#if UNITY_2019_1_OR_NEWER
            isRowDataEditorOpening = EditorWindow.HasOpenInstances<RowDataEditor>();
#else
            isRowDataEditorOpening = Resources.FindObjectsOfTypeAll<RowDataEditor>().Length > 0
#endif
            if (isRowDataEditorOpening)
            {
                TableViewItem<T> item = _items.Find((it) => { return it.id == selectedIds[0]; });
                RowDataEditor.OpenWindow(target as UnityEngine.Object, _datas.IndexOf(item.Data));
            }
        }

        /// <summary>
        /// 是否允许多选
        /// </summary>
        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return IsCanMultiSelect;
        }

        /// <summary>
        /// 绘制表格视图
        /// </summary>
        /// <param name="rect">绘制区域</param>
        public override void OnGUI(Rect rect)
        {
            if (IsEnableSearch)
            {
                Rect sub = new Rect(rect.x, rect.y, 60, 16);
                EditorGUI.LabelField(sub, "Search: ");
                sub.Set(rect.x + 60, rect.y, rect.width - 60, 18);
                searchString = _searchField.OnGUI(sub, searchString);

                rect.y += 18;
                rect.height -= 18;
                base.OnGUI(rect);
            }
            else
            {
                base.OnGUI(rect);
            }

            realTreeViewRect = rect;
        }

        /// <summary>
        /// 获取索引列
        /// </summary>
        private TableColumn<T> GetIndexColumn()
        {
            TableColumn<T> column = new TableColumn<T>();
            column.autoResize = false;
            column.headerContent = new GUIContent("Index");
            column.width = 50;
            column.canSort = false;
            column.Compare = null;
            column.DrawCell = (rect, data, rowIndex, isSelected, isFocused) =>
            {
                EditorGUI.LabelField(rect, rowIndex.ToString());
            };
            return column;
        }

        /// <summary>
        /// 当重新排序
        /// </summary>
        private void OnSortingChanged(MultiColumnHeader columnheader)
        {
            bool isAscending = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);
            TableColumn<T> column = multiColumnHeader.GetColumn(multiColumnHeader.sortedColumnIndex) as TableColumn<T>;
            if (column.Compare != null)
            {
                _items.Sort((a, b) =>
                {
                    if (isAscending)
                    {
                        return -column.Compare(a.Data, b.Data);
                    }
                    else
                    {
                        return column.Compare(a.Data, b.Data);
                    }
                });
                Reload();
            }
        }

        /// <summary>
        /// 当列激活状态改变
        /// </summary>
        private void OnVisibleColumnsChanged(MultiColumnHeader columnheader)
        {
            Reload();
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="data">数据</param>
        public void AddData(T data)
        {
            if (_datas.Contains(data))
                return;

            _datas.Add(data);
            _items.Add(new TableViewItem<T>(_idSign, 0, data));
            _idSign += 1;
            Reload();
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="datas">数据</param>
        public void AddDatas(List<T> datas)
        {
            for (int i = 0; i < datas.Count; i++)
            {
                T data = datas[i];
                if (_datas.Contains(data))
                    continue;

                _datas.Add(data);
                _items.Add(new TableViewItem<T>(_idSign, 0, data));
                _idSign += 1;
            }
            Reload();
        }

        /// <summary>
        /// 删除数据
        /// </summary>
        /// <param name="data">数据</param>
        public void DeleteData(T data)
        {
            if (!_datas.Contains(data))
                return;

            _datas.Remove(data);
            TableViewItem<T> item = _items.Find((i) => { return i.Data == data; });
            if (item != null)
            {
                _items.Remove(item);
            }
            Reload();
        }

        /// <summary>
        /// 删除数据
        /// </summary>
        /// <param name="datas">数据</param>
        public void DeleteDatas(List<T> datas)
        {
            for (int i = 0; i < datas.Count; i++)
            {
                T data = datas[i];
                if (!_datas.Contains(data))
                    continue;

                _datas.Remove(data);
                TableViewItem<T> item = _items.Find((t) => { return t.Data == data; });
                if (item != null)
                {
                    _items.Remove(item);
                }
            }
            Reload();
        }

        /// <summary>
        /// 选中数据
        /// </summary>
        /// <param name="data">数据</param>
        public void SelectData(T data)
        {
            if (!_datas.Contains(data))
                return;

            TableViewItem<T> item = _items.Find((i) => { return i.Data == data; });
            if (item != null)
            {
                SetSelection(new int[] { item.id });
            }
        }

        /// <summary>
        /// 选中数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="options">选中的操作</param>
        public void SelectData(T data, TreeViewSelectionOptions options)
        {
            if (!_datas.Contains(data))
                return;

            TableViewItem<T> item = _items.Find((i) => { return i.Data == data; });
            if (item != null)
            {
                SetSelection(new int[] { item.id }, options);
            }
        }

        /// <summary>
        /// 选中数据
        /// </summary>
        /// <param name="datas">数据</param>
        public void SelectDatas(List<T> datas)
        {
            List<int> ids = new List<int>();
            for (int i = 0; i < datas.Count; i++)
            {
                T data = datas[i];
                if (!_datas.Contains(data))
                    continue;

                TableViewItem<T> item = _items.Find((t) => { return t.Data == data; });
                if (item != null)
                {
                    ids.Add(item.id);
                }
            }

            if (ids.Count > 0)
            {
                SetSelection(ids);
            }
        }

        /// <summary>
        /// 选中数据
        /// </summary>
        /// <param name="datas">数据</param>
        /// <param name="options">选中的操作</param>
        public void SelectDatas(List<T> datas, TreeViewSelectionOptions options)
        {
            List<int> ids = new List<int>();
            for (int i = 0; i < datas.Count; i++)
            {
                T data = datas[i];
                if (!_datas.Contains(data))
                    continue;

                TableViewItem<T> item = _items.Find((t) => { return t.Data == data; });
                if (item != null)
                {
                    ids.Add(item.id);
                }
            }

            if (ids.Count > 0)
            {
                SetSelection(ids, options);
            }
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void ClearData()
        {
            _datas.Clear();
            _items.Clear();
            Reload();
        }

        // 绘制表头ToolTip
        public void DrawHeaderToolTips()
        {
            if (multiColumnHeader == null || Event.current.type != EventType.Repaint) return;

            Vector2 mousePosition = Event.current.mousePosition;
            mousePosition.x += state.scrollPos.x;   // 计算坐标时要加上Treeview的scrollPos的x值,不然判断会出错
            hoveredColumnIndex = -1;

            // 计算表头的实际窗口位置
            Rect headerRect = new Rect(
                realTreeViewRect.x + state.scrollPos.x,
                realTreeViewRect.y,
                realTreeViewRect.width,
                multiColumnHeader.height
            );

            // 检查鼠标是否在表头区域
            if (headerRect.Contains(mousePosition))
            {
                // 检测悬停的具体列
                for (int i = 0; i < multiColumnHeader.state.columns.Length; i++)
                {
                    // 跳过不可见的列
                    if (!multiColumnHeader.IsColumnVisible(i)) continue;

                    Rect columnRect = multiColumnHeader.GetColumnRect(i);
                    // 转换为窗口绝对坐标
                    columnRect.x += realTreeViewRect.x;
                    columnRect.y += realTreeViewRect.y;

                    if (columnRect.Contains(mousePosition))
                    {
                        hoveredColumnIndex = i;
                        break;
                    }
                }
            }

            // 显示Tooltip ，index = 0是行号，所以从1开始
            if (hoveredColumnIndex > 0)
            {
                string tooltip = ColumnToolTip[hoveredColumnIndex - 1];
                if (!string.IsNullOrEmpty(tooltip))
                {
                    ShowTooltip(Event.current.mousePosition, tooltip);  //用原始的鼠标坐标来绘制
                }
            }
        }

        private void ShowTooltip(Vector2 position, string text)
        {
            // 创建Tooltip样式
            GUIStyle tooltipStyle = new GUIStyle("tooltip")
            {
                padding = new RectOffset(10, 10, 5, 5),
                fontSize = 11,
                wordWrap = true
            };

            // 计算Tooltip大小
            GUIContent content = new GUIContent(text);
            Vector2 size = tooltipStyle.CalcSize(content);
            size.x = Mathf.Min(size.x, 300); // 限制最大宽度

            // 位置偏移（防止遮挡鼠标）
            Rect tooltipRect = new Rect(
                position.x + 15,
                position.y + 15,
                size.x + 20,
                tooltipStyle.CalcHeight(content, size.x) + 10
            );

            // 绘制Tooltip
            GUI.Label(tooltipRect, text, tooltipStyle);
        }
    }
}