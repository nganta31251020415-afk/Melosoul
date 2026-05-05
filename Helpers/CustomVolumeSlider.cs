using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Melosoul.Helpers
{
    public class CustomVolumeSlider : Panel
    {
        private const int TrackHeight = 4;
        private const int ThumbDiameter = 14;
        private const int ThumbBorderWidth = 2;

        private readonly Color _trackColor = Color.FromArgb(0x3A, 0x3A, 0x3A);
        private readonly Color _fillColor = Color.FromArgb(0xE9, 0x1E, 0x8C);
        private readonly Color _thumbFillColor = Color.White;
        private readonly Color _thumbHoverFillColor = Color.FromArgb(0xFF, 0x6B, 0x9D);
        private readonly Color _backgroundColor = Color.FromArgb(0x12, 0x12, 0x12);

        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private bool _isDragging;
        private bool _isHoveringThumb;

        public CustomVolumeSlider()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            BackColor = _backgroundColor;
            Cursor = Cursors.Hand;
        }

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return _minimum; }
            set
            {
                _minimum = value;
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
                _maximum = Math.Max(value, _minimum);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            Rectangle trackBounds = GetTrackBounds();
            float thumbCenterX = GetThumbCenterX(trackBounds);
            Rectangle fillBounds = Rectangle.FromLTRB(
                trackBounds.Left,
                trackBounds.Top,
                (int)Math.Round(thumbCenterX),
                trackBounds.Bottom);
            RectangleF thumbBounds = new RectangleF(
                thumbCenterX - ThumbDiameter / 2f,
                Height / 2f - ThumbDiameter / 2f,
                ThumbDiameter,
                ThumbDiameter);

            using (GraphicsPath trackPath = CreateRoundedRectPath(trackBounds, TrackHeight / 2f))
            using (Brush trackBrush = new SolidBrush(_trackColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            if (fillBounds.Width > 0)
            {
                using (GraphicsPath fillPath = CreateRoundedRectPath(fillBounds, TrackHeight / 2f))
                using (Brush fillBrush = new SolidBrush(_fillColor))
                {
                    e.Graphics.FillPath(fillBrush, fillPath);
                }
            }

            using (Brush thumbBrush = new SolidBrush(_isHoveringThumb || _isDragging ? _thumbHoverFillColor : _thumbFillColor))
            using (Pen thumbBorder = new Pen(_fillColor, ThumbBorderWidth))
            {
                e.Graphics.FillEllipse(thumbBrush, thumbBounds);
                e.Graphics.DrawEllipse(thumbBorder, thumbBounds);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            _isDragging = true;
            Capture = true;
            SetValueFromMouse(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            bool wasHovering = _isHoveringThumb;
            _isHoveringThumb = GetThumbBounds(GetTrackBounds()).Contains(e.Location);
            if (wasHovering != _isHoveringThumb)
                Invalidate();

            if (_isDragging)
                SetValueFromMouse(e.X);
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

        private Rectangle GetTrackBounds()
        {
            int horizontalPadding = ThumbDiameter / 2;
            int y = Height / 2 - TrackHeight / 2;
            return new Rectangle(
                horizontalPadding,
                y,
                Math.Max(1, Width - horizontalPadding * 2),
                TrackHeight);
        }

        private RectangleF GetThumbBounds(Rectangle trackBounds)
        {
            float thumbCenterX = GetThumbCenterX(trackBounds);
            return new RectangleF(
                thumbCenterX - ThumbDiameter / 2f,
                Height / 2f - ThumbDiameter / 2f,
                ThumbDiameter,
                ThumbDiameter);
        }

        private float GetThumbCenterX(Rectangle trackBounds)
        {
            if (_maximum == _minimum)
                return trackBounds.Left;

            float ratio = (float)(_value - _minimum) / (_maximum - _minimum);
            return trackBounds.Left + ratio * trackBounds.Width;
        }

        private void SetValueFromMouse(int mouseX)
        {
            Rectangle trackBounds = GetTrackBounds();
            float ratio = (float)(mouseX - trackBounds.Left) / trackBounds.Width;
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            Value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;

            radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
            float diameter = radius * 2f;

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 90, 180);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 180);
            path.CloseFigure();
            return path;
        }
    }
}
