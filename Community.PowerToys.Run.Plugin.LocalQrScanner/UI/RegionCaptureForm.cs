using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Community.PowerToys.Run.Plugin.LocalQrScanner.UI;

internal sealed class RegionCaptureForm : Form
{
    private const int MinimumSelectionSize = 6;
    private readonly Bitmap desktopImage;
    private readonly Rectangle virtualScreen;
    private Point dragStart;
    private Rectangle selection;
    private bool isDragging;
    private volatile bool cancelRequested;

    private RegionCaptureForm()
    {
        virtualScreen = SystemInformation.VirtualScreen;
        desktopImage = CaptureDesktop(virtualScreen);

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Bounds = virtualScreen;
    }

    public Bitmap? SelectedBitmap { get; private set; }

    public static Task<Bitmap?> CaptureAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var form = new RegionCaptureForm();
                using var registration = cancellationToken.Register(form.RequestCancel);
                var result = form.ShowDialog();
                completion.TrySetResult(result == DialogResult.OK ? form.SelectedBitmap : null);
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Local QR Scanner region capture",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    internal static Rectangle NormalizeSelection(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        return new Rectangle(left, top, Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (cancelRequested)
        {
            CancelCapture();
            return;
        }

        Activate();
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CancelCapture();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            CancelCapture();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            dragStart = e.Location;
            selection = Rectangle.Empty;
            isDragging = true;
            Capture = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (isDragging)
        {
            selection = Rectangle.Intersect(ClientRectangle, NormalizeSelection(dragStart, e.Location));
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && isDragging)
        {
            isDragging = false;
            Capture = false;
            selection = Rectangle.Intersect(ClientRectangle, NormalizeSelection(dragStart, e.Location));
            if (selection.Width >= MinimumSelectionSize && selection.Height >= MinimumSelectionSize)
            {
                SelectedBitmap = desktopImage.Clone(selection, PixelFormat.Format32bppArgb);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            selection = Rectangle.Empty;
            Invalidate();
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(desktopImage, Point.Empty);
        using (var shade = new SolidBrush(Color.FromArgb(118, Color.Black)))
        {
            e.Graphics.FillRectangle(shade, ClientRectangle);
        }

        if (!selection.IsEmpty)
        {
            e.Graphics.DrawImage(desktopImage, selection, selection, GraphicsUnit.Pixel);
            using var border = new Pen(Color.FromArgb(0, 174, 255), 2f);
            e.Graphics.DrawRectangle(border, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);
        }

        DrawInstructions(e.Graphics);
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            desktopImage.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Bitmap CaptureDesktop(Rectangle bounds)
    {
        var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        return image;
    }

    private void DrawInstructions(Graphics graphics)
    {
        const string instructions = "拖动鼠标框选二维码区域  ·  Esc 或右键取消";
        var primary = Screen.PrimaryScreen?.Bounds ?? virtualScreen;
        var size = TextRenderer.MeasureText(instructions, Font);
        var box = new Rectangle(
            primary.Left - virtualScreen.Left + ((primary.Width - size.Width) / 2) - 18,
            primary.Top - virtualScreen.Top + 28,
            size.Width + 36,
            size.Height + 18);
        using var background = new SolidBrush(Color.FromArgb(205, 24, 24, 24));
        graphics.FillRectangle(background, box);
        TextRenderer.DrawText(
            graphics,
            instructions,
            Font,
            box,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void RequestCancel()
    {
        cancelRequested = true;
        if (!IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(CancelCapture);
        }
        catch (InvalidOperationException)
        {
            // The selection window finished while cancellation was being dispatched.
        }
    }

    private void CancelCapture()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
