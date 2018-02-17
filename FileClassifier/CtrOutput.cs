using System;
using System.Collections.Generic;
using System.Drawing;
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
            };
            _listBox.MouseDoubleClick += _listBox_MouseDoubleClick;
            Controls.Add(_listBox);

            _timer = new Timer
            {
                Interval = 100,
                Enabled = true
            };
            _timer.Tick += _timer_Tick;
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
                _listBox.SuspendLayout();
                foreach (OutputItem item in _pendingOutput)
                {
                    _listBox.Items.Add(item);
                }
                _pendingOutput.Clear();
                _listBox.ResumeLayout();
            }

            Application.DoEvents();

            int visibleItems = _listBox.ClientSize.Height / _listBox.ItemHeight;
            _listBox.TopIndex = Math.Max(_listBox.Items.Count - visibleItems + 1, 0);
            _updated = false;
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
            //if (_listBox.InvokeRequired)
            //{
            //    _listBox.Invoke((MethodInvoker)(() =>
            //    {
            //        _listBox.Items.Add(new OutputItem { Text = line, Data = data, });
            //        _updated = true;
            //    }));
            //}
            //else
            //{
            //    _listBox.Items.Add(new OutputItem { Text = line, Data = data, });
            //    _updated = true;
            //}
        }
        
        public string GetCurrentText()
        {
            OutputItem item = (OutputItem)_listBox.SelectedItem;
            return item?.Text;
        }

        public object GetCurrentData()
        {
            OutputItem item = (OutputItem)_listBox.SelectedItem;
            return item?.Data;
        }
    }
}
