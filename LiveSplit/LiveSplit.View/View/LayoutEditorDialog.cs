﻿using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.View
{
    public partial class LayoutEditorDialog : Form
    {
        public event EventHandler OrientationSwitched;
        public event EventHandler LayoutResized;
        //public event EventHandler LayoutSizeChanged;
        public event EventHandler LayoutSettingsAssigned;

        public Form Form { get; set; }

        public LiveSplitState CurrentState { get; set; }

        public List<UI.Components.IComponent> ComponentsToDispose { get; set; }

        protected ILayout Layout { get; set; }
        protected BindingList<ILayoutComponent> BindingList { get; set; }
        protected float OverallHeight { get { return BindingList.OfType<ILayoutComponent>().Aggregate(0.0f, (x, y) => x + y.Component.VerticalHeight); } }
        protected bool IsVertical
        {
            get { return Layout.Mode == LayoutMode.Vertical; }
            set
            {
                if (value)
                    Layout.Mode = LayoutMode.Vertical;
                else
                    Layout.Mode = LayoutMode.Horizontal;
            }
        }
        protected bool IsHorizontal { get { return !IsVertical; } set { IsVertical = !value; } }
        public LayoutEditorDialog(ILayout layout, LiveSplitState state, Form form)
        {
            InitializeComponent();
            Form = form;
            Layout = layout;
            BindingList = new BindingList<ILayoutComponent>(Layout.LayoutComponents);
            ComponentsToDispose = new List<UI.Components.IComponent>();
            lbxComponents.DataSource = BindingList;
            lbxComponents.DisplayMember = "Component.ComponentName";
            LoadAllComponentsAvailable();

            rdoVertical.Checked = IsVertical;
            rdoHorizontal.Checked = IsHorizontal;
            rdoVertical.CheckedChanged += rdoVertical_CheckedChanged;

            CurrentState = state;
            var itemDragger = new ListBoxItemDragger(lbxComponents, form);
            itemDragger.DragCursor = Cursors.SizeAll;
        }

        void rdoVertical_CheckedChanged(object sender, EventArgs e)
        {
            Layout.HasChanged = true;
            IsVertical = rdoVertical.Checked;
            IsHorizontal = !rdoVertical.Checked;
            if (OrientationSwitched != null)
                OrientationSwitched(this, null);
        }

        private void AddComponent(IComponentFactory factory)
        {
            //if (!CurrentState.DrawLock.TryEnterWriteLock(500))
                //return;
            try
            {
                float previousHeight = OverallHeight;
                try
                {
                    var componentFactory = ComponentManager.ComponentFactories.FirstOrDefault(x => x.Value.ComponentName == factory.ComponentName);
                    var component = componentFactory.Value == null
                        ? new LayoutComponent("", new SeparatorComponent())
                        : new LayoutComponent(componentFactory.Key, componentFactory.Value.Create(CurrentState));
                    Action y = () =>
                    {
                        try
                        {
                            BindingList.Add(component);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    };

                    if (Form.InvokeRequired)
                        Form.Invoke(y);
                    else
                        y();
                    Layout.HasChanged = true;
                    if (LayoutResized != null)
                        LayoutResized(this, null);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    MessageBox.Show(this, "The Component could not be loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                //CurrentState.DrawLock.ExitWriteLock();
            }
        }

        private void AddComponentFactory(IComponentFactory factory, ToolStripMenuItem menuItem)
        {
            var item = new ToolStripMenuItem(factory.ComponentName);
                    item.Click += (s, e) =>
                        {
                            AddComponent(factory);
                        };
                    item.ToolTipText = factory.Description;
                    menuItem.DropDownItems.Add(item);
        }

        private void AddGroup(ComponentCategory groupCategory, IEnumerable<IComponentFactory> factories)
        {
            var groupItem = new ToolStripMenuItem(groupCategory.ToString());
            foreach (var factory in factories)
            {
                AddComponentFactory(factory, groupItem);
            }
            menuAddComponents.Items.Add(groupItem);
        }

        private void LoadAllComponentsAvailable()
        {
            var autosplitters = AutoSplitterFactory.Instance.AutoSplitters != null
                ? AutoSplitterFactory.Instance.AutoSplitters.Where(x => !x.Value.ShowInLayoutEditor).Select(x => x.Value.FileName)
                : new List<string>();
            var groups = ComponentManager.ComponentFactories.Where(x => !autosplitters.Contains(x.Key)).Select(x => x.Value).GroupBy(x => x.Category, x => x).OrderBy(x => x.Key);
            foreach (var group in groups)
            {
                var category = group.Key;
                var componentFactories = (IEnumerable<IComponentFactory>)group;
                if (category == ComponentCategory.Other)
                    componentFactories = new[] { new SeparatorFactory() }.Concat(componentFactories).OrderBy(x => x.ComponentName);
                AddGroup(category, componentFactories);
            }

            menuAddComponents.Items.Add(new ToolStripSeparator());
            var downloadMore = new ToolStripMenuItem("Download More...");
            downloadMore.Click += (s, e) =>
                {
                    Process.Start("http://livesplit.org/components/");
                };

            menuAddComponents.Items.Add(downloadMore);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            menuAddComponents.Show(this, new Point(btnAdd.Width + btnAdd.Location.X, btnAdd.Location.Y));
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            //if (!CurrentState.DrawLock.TryEnterWriteLock(500))
            //return;
            if (BindingList.Count > 1)
            {
                float previousHeight = OverallHeight;
                Action x = () =>
                {
                    try
                    {
                        var component = Layout.Components.ElementAt(lbxComponents.SelectedIndex);
                        if (component is IDeactivatableComponent)
                            ((IDeactivatableComponent)component).Activated = false;
                        ComponentsToDispose.Add(component);
                        BindingList.RemoveAt(lbxComponents.SelectedIndex);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                };

                if (Form.InvokeRequired)
                    Form.Invoke(x);
                else
                    x();
                Layout.HasChanged = true;
                if (LayoutResized != null)
                    LayoutResized(this, null);
            }
            //CurrentState.DrawLock.ExitWriteLock();
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            //if (!CurrentState.DrawLock.TryEnterWriteLock(500))
            //return;
            if (lbxComponents.SelectedIndex > 0)
            {
                Action x = () =>
                {
                    try
                    {
                        BindingList.Insert(lbxComponents.SelectedIndex - 1, BindingList[lbxComponents.SelectedIndex]);
                        lbxComponents.SelectedIndex -= 2;
                        BindingList.RemoveAt(lbxComponents.SelectedIndex + 2);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                };

                if (Form.InvokeRequired)
                    Form.Invoke(x);
                else
                    x();

                Layout.HasChanged = true;
            }
            //CurrentState.DrawLock.ExitWriteLock();
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            //if (!CurrentState.DrawLock.TryEnterWriteLock(500))
            //return;
            if (lbxComponents.SelectedIndex < BindingList.Count - 1)
            {
                Action x = () =>
                {
                    try
                    {
                        BindingList.Insert(lbxComponents.SelectedIndex + 2, BindingList[lbxComponents.SelectedIndex]);
                        BindingList.RemoveAt(lbxComponents.SelectedIndex);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                };

                if (Form.InvokeRequired)
                    Form.Invoke(x);
                else
                    x();
                lbxComponents.SelectedIndex += 1;
                Layout.HasChanged = true;
            }
            //CurrentState.DrawLock.ExitWriteLock();
        }

        private void ShowLayoutSettings(LiveSplit.UI.Components.IComponent tabControl = null)
        {
            var oldSettings = (Options.LayoutSettings)Layout.Settings.Clone();
            var settingsDialog = new LayoutSettingsDialog(Layout.Settings, Layout, tabControl);
            var result = settingsDialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                Layout.HasChanged = true;
            }
            else if (result == DialogResult.Cancel)
            {
                Layout.Settings.Assign(oldSettings);
                LayoutSettingsAssigned(null, null);
            }
            BindingList.ResetBindings();
        }

        private void btnLayoutSettings_Click(object sender, EventArgs e)
        {
            ShowLayoutSettings();
        }

        private void btnSetSize_Click(object sender, EventArgs e)
        {
            var setSizeDialog = new SetSizeForm(CurrentState.Form);
            var oldSize = CurrentState.Form.Size;
            var result = setSizeDialog.ShowDialog();

            if (result == DialogResult.Cancel)
                CurrentState.Form.Size = oldSize;
        }

        private void lbxComponents_DoubleClick(object sender, EventArgs e)
        {
        }

        private void lbxComponents_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = lbxComponents.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                var selectedItem = lbxComponents.Items[index];
                try
                {
                    var control = ((ILayoutComponent)selectedItem).Component;
                    if (control.GetSettingsControl(Layout.Mode) != null)
                    {
                        ShowLayoutSettings(((ILayoutComponent)selectedItem).Component);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }
}
