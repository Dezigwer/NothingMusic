#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace NothingMusic
{
    /// <summary>
    /// Панель с двойной буферизацией — без мерцания и «призраков» при перерисовке.
    /// </summary>
    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.FromArgb(38, 38, 38);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // не прокручиваем содержимое, когда курсор над слайдером —
            // слайдер обрабатывает колесо в своей зоне у ползунка
            Point client = PointToClient(MousePosition);
            Control? child = GetChildAtPoint(client);
            if (child is AccentSlider)
                return;

            base.OnMouseWheel(e);
        }
    }
}
