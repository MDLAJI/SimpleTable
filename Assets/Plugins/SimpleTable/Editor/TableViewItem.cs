/*
 * 此文件基于原项目 HTFramework (https://github.com/SaiTingHu/HTFramework) 的代码修改
 * 原文件版权归原作者所有，遵循MIT许可证。
 * 
 * 原始版权声明：
 * Copyright (c) 2019 HuTao
 * 
 */

using UnityEditor.IMGUI.Controls;

namespace SimpleTable.Editor
{
    /// <summary>
    /// 表格视图元素
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public sealed class TableViewItem<T> : TreeViewItem where T : class, new()
    {
        /// <summary>
        /// 元素的数据
        /// </summary>
        public T Data { get; private set; }

        public TableViewItem(int id, int depth, T data) : base(id, depth, data == null ? "Root" : data.ToString())
        {
            Data = data;
        }
    }
}