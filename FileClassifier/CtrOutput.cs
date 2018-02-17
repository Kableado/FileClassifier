using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FileClassifier
{
    class CtrOutput : Control
    {
        private ListBox _listBox;

        private Timer _timer;

        private class OutputItem
        {
            public string Text { get; set; }
            public object Data { get; set; }
            public override string ToString()
            {
                return Text;
            }
        }

        public new event EventHandler DoubleClick;

        public CtrOutput()
        {
            InitializeControls();
        }

        private void InitializeControls()
        {
            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                FormattingEnabled = true,
                Font = new System.Drawing.Font("Consolas", 9),
                BackColor = Color.Black,
                ForeColor = Color.Gray,
                SelectionMode = SelectionMode.MultiExtended,
            };
            _listBox.MouseDoubleClick += _listBox_MouseDoubleClick;
            _listBox.KeyDown += _listBox_KeyDown;
            Controls.Add(_listBox);

            _timer = new Timer
            {
                Interval = 100,
                Enabled = true
            };
            _timer.Tick += _timer_Tick;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.C) == Keys.C)
            {
                CopyToClipboard();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void _listBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Control && e.KeyCode == Keys.C)
            {
                CopyToClipboard();
            }
        }

        private void CopyToClipboard()
        {
            StringBuilder sbText = new StringBuilder();
            foreach (OutputItem item in _listBox.SelectedItems)
            {
                sbText.AppendLine(item.Text);
            }
            if (sbText.Length > 0)
            {
                Clipboard.SetText(sbText.ToString());
            }
        }

        private void _listBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            DoubleClick?.Invoke(sender, e);
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            if (_updated)
            {
                UpdatePosition();
            }
        }
        
        private bool _updated = false;
        private List<OutputItem> _pendingOutput = new List<OutputItem>();

        private void UpdatePosition()
        {
            lock (_pendingOutput)
            {
                EnableRepaint(new HandleRef(_listBox, _listBox.Handle), false);
                _listBox.SuspendLayout();
                foreach (OutputItem item in _pendingOutput)
                {
                    _listBox.Items.Add(item);
                }
                _pendingOutput.Clear();
                _listBox.ResumeLayout();

                int visibleItems = _listBox.ClientSize.Height / _listBox.ItemHeight;
                _listBox.TopIndex = Math.Max(_listBox.Items.Count - visibleItems + 1, 0);
                _updated = false;
                EnableRepaint(new HandleRef(_listBox, _listBox.Handle), true);
                _listBox.Invalidate();
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(HandleRef hWnd, Int32 Msg, IntPtr wParam, IntPtr lParam);

        private static void EnableRepaint(HandleRef handle, bool enable)
        {
            const int WM_SETREDRAW = 0x000B;
            SendMessage(handle, WM_SETREDRAW, new IntPtr(enable ? 1 : 0), IntPtr.Zero);
        }

        public void Clean()
        {
            if (_listBox.InvokeRequired)
            {
                _listBox.Invoke((MethodInvoker)(() =>
                {
                    _listBox.Items.Clear();
                    _updated = true;
                }));
            }
            else
            {
                _listBox.Items.Clear();
                _updated = true;
            }
        }

        public void AddLine(string line, object data = null)
        {
            lock (_pendingOutput)
            {
                _pendingOutput.Add(new OutputItem { Text = line, Data = data, });
                _updated = true;
            }
        }
        
        public string GetCurrentText()
        {
            if (_listBox.SelectedItems.Count == 0) { return null; }
            OutputItem item = (OutputItem)_listBox.SelectedItems[0];
            return item?.Text;
        }

        public object GetCurrentData()
        {
            if (_listBox.SelectedItems.Count == 0) { return null; }
            OutputItem item = (OutputItem)_listBox.SelectedItems[0];
            return item?.Data;
        }
    }
}
