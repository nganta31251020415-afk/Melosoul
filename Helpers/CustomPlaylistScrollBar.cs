using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Melosoul.Helpers
{
    public class CustomPlaylistScrollBar : Control
    {
        private const int TrackPadding = 3;
        private const int MinThumbHeight = 34;

        private readonly Color _trackColor = Color.FromArgb(24, 18, 23);
        private readonly Color _thumbColor = Color.FromArgb(116, 48, 86);
        private readonly Color _thumbHoverColor = Color.FromArgb(160, 76, 116);
        private readonly Color _thumbDragColor = Color.FromArgb(233, 30, 140);

        private int _minimum;
        private int _maximum;
        private int _value;
        private int _viewportSize = 1;
        private bool _isDragging;
        private bool _isHoveringThumb;
        private int _dragOffsetY;

        public CustomPlaylistScrollBar()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            Width = 12;
            Cursor = Cursors.Hand;
        }

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return _minimum; }
            set
            {
                _minimum = Math.Max(0, value);
                if (_maximum < _minimum)
                    _maximum = _minimum;
                Value = _value;
                Invalidate();
            }
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                _maximum = Math.Max(_minimum, value);
                Value = _value;
                Invalidate();
            }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int newValue = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value == newValue)
                    return;

                _value = newValue;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int ViewportSize
        {
            get { return _viewportSize; }
            set
            {
                _viewportSize = Math.Max(1, value);
                Invalidate();
            }
        }

        public void SetScrollInfo(int minimum, int maximum, int value, int viewportSize)
        {
            _minimum = Math.Max(0, minimum);
            _maximum = Math.Max(_minimum, maximum);
            _viewportSize = Math.Max(1, viewportSize);

            int newValue = Math.Max(_minimum, Math.Min(_maximum, value));
            bool changed = _value != newValue;
            _value = newValue;

            Invalidate();
            if (changed)
                ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(18, 18, 18));

            Rectangle trackBounds = GetTrackBounds();
            using (GraphicsPath trackPath = CreateRoundedRectPath(trackBounds, trackBounds.Width / 2f))
            using (Brush trackBrush = new SolidBrush(_trackColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            if (_maximum <= _minimum)
                return;

            Rectangle thumbBounds = GetThumbBounds();
            Color thumbColor = _isDragging
                ? _thumbDragColor
                : (_isHoveringThumb ? _thumbHoverColor : _thumbColor);

            using (GraphicsPath thumbPath = CreateRoundedRectPath(thumbBounds, thumbBounds.Width / 2f))
            using (Brush thumbBrush = new SolidBrush(thumbColor))
            {
                e.Graphics.FillPath(thumbBrush, thumbPath);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _maximum <= _minimum)
                return;

            Rectangle thumbBounds = GetThumbBounds();
            if (thumbBounds.Contains(e.Location))
            {
                _isDragging = true;
                _dragOffsetY = e.Y - thumbBounds.Top;
                Capture = true;
                Invalidate();
                return;
            }

            Value += e.Y < thumbBounds.Top ? -ViewportSize : ViewportSize;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging)
            {
                SetValueFromThumbTop(e.Y - _dragOffsetY);
                return;
            }

            bool isHovering = GetThumbBounds().Contains(e.Location);
            if (_isHoveringThumb != isHovering)
            {
                _isHoveringThumb = isHovering;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            _isDragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isDragging)
                return;

            _isHoveringThumb = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value -= Math.Sign(e.Delta) * 3;
        }

        private Rectangle GetTrackBounds()
        {
            return new Rectangle(
                TrackPadding,
                TrackPadding,
                Math.Max(1, Width - TrackPadding * 2),
                Math.Max(1, Height - TrackPadding * 2));
        }

        private Rectangle GetThumbBounds()
        {
            Rectangle trackBounds = GetTrackBounds();
            if (_maximum <= _minimum)
                return new Rectangle(trackBounds.Left, trackBounds.Top, trackBounds.Width, trackBounds.Height);

            int range = _maximum - _minimum + _viewportSize;
            int thumbHeight = Math.Max(MinThumbHeight,
                (int)Math.Round((double)_viewportSize / range * trackBounds.Height));
            thumbHeight = Math.Min(trackBounds.Height, thumbHeight);

            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            double ratio = (double)(_value - _minimum) / (_maximum - _minimum);
            int top = trackBounds.Top + (int)Math.Round(ratio * travel);

            return new Rectangle(trackBounds.Left, top, trackBounds.Width, thumbHeight);
        }

        private void SetValueFromThumbTop(int thumbTop)
        {
            Rectangle trackBounds = GetTrackBounds();
            Rectangle thumbBounds = GetThumbBounds();
            int travel = Math.Max(1, trackBounds.Height - thumbBounds.Height);
            int clampedTop = Math.Max(trackBounds.Top, Math.Min(trackBounds.Bottom - thumbBounds.Height, thumbTop));
            double ratio = (double)(clampedTop - trackBounds.Top) / travel;
            Value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;

            radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
            float diameter = radius * 2f;

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
