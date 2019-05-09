using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Shapes;
using Point = System.Windows.Point;
using PointF = System.Drawing.PointF;

namespace SketchToAI
{
    public partial class MainWindow : Window
    {
        public int CanvasSize { get; set; } = 28 * 4;
        public int OutputSize { get; set; } = 28;
        public float PenSize { get; set; } = 1.33f;
        public float EraserSize { get; set; } = 3;
        private bool imageReady => _currentOutput != null; 

        private object _lock = new object();
        private CancellationTokenSource _closing = new CancellationTokenSource();
        private CliServerHost _analyzer;
        private Image<Gray8> _canvas;
        private byte[] _currentOutput;
        private byte[] _analyzedOutput;
        private HashSet<MouseButton> _buttonsPressed = new HashSet<MouseButton>();

        public MainWindow()
        {
            InitializeComponent();
            ParseCommandLineArguments();
            UpdateImage(new Point());
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            _closing?.Cancel();
            _analyzer?.Dispose();
        }

        private void ParseCommandLineArguments()
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToList();

            void Consume(int count = 1)
            {
                for (int i = 0; i < count; i++)
                    args.RemoveAt(0);
            }

            void CheckSwitch(string shortName, string fullName, Action apply, int consumeOnApply = 2)
            {
                if (args.Count < 1)
                    return;
                if ((!string.IsNullOrEmpty(shortName) && args[0].ToLowerInvariant() == "-" + shortName.ToLowerInvariant()) ||
                    (!string.IsNullOrEmpty(fullName) && args[0].ToLowerInvariant() == "--" + fullName.ToLowerInvariant())) {
                    apply.Invoke();
                    Consume(consumeOnApply);
                }
            }

            void Help()
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  sketchToAI [--canvasSize M] [--outputSize N] analyzerToUse.exe [analyzer arguments]");
                Console.WriteLine();
                Dispatcher.Invoke(Close);
            }

            CheckSwitch("?", "help", Help);
            CheckSwitch("cs", "canvasSize", () => CanvasSize = int.Parse(args[1]));
            CheckSwitch("os", "outputSize", () => OutputSize = int.Parse(args[1]));
            CheckSwitch("ps", "penSize",    () => PenSize =    int.Parse(args[1]));
            CheckSwitch("es", "EraserSize", () => EraserSize = int.Parse(args[1]));

            
            try {
                var analyzerCommand = args[0];
                var analyzerArguments = string.Join(" ", args.Skip(1).ToArray());
                _analyzer = new CliServerHost(analyzerCommand, analyzerArguments).Start();
            }
            catch (Exception e) {
                Console.WriteLine($"Error: {e.Message}");
                Console.WriteLine($"Likely, invalid arguments.");
                Console.WriteLine();
                Help();
            }
        }

        private void Image_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _buttonsPressed.Add(e.ChangedButton);
            UpdateImage(e.GetPosition(Image));
        }

        private void Image_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _buttonsPressed.Remove(e.ChangedButton);
            UpdateImage(e.GetPosition(Image));
        }

        private void Image_OnMouseMove(object sender, MouseEventArgs e)
        {
            UpdateImage(e.GetPosition(Image));
        }

        private void UpdateImage(Point mousePosition)
        {
            var penCenter = new PointF(
                (float) (CanvasSize * mousePosition.X / Image.ActualWidth), 
                (float) (CanvasSize * mousePosition.Y / Image.ActualHeight));
            var penColor = new Gray8(0);
            var penSize = PenSize * CanvasSize / OutputSize / 2;
            var eraserColor = new Gray8(255);
            var eraserSize =  EraserSize * CanvasSize / OutputSize / 2;

            if (_buttonsPressed.Contains(MouseButton.Left))
                _canvas.Mutate(i => i.Fill(penColor, new EllipsePolygon(penCenter.X, penCenter.Y, penSize)));
            else if (_buttonsPressed.Contains(MouseButton.Right))
                _canvas.Mutate(i => i.Fill(eraserColor, new EllipsePolygon(penCenter.X, penCenter.Y, eraserSize)));
            else {
                if (_canvas != null)
                    return; // Nothing changed
                _canvas = new Image<Gray8>(CanvasSize, CanvasSize);
                _canvas.Mutate(i => i.Fill(new Gray8(255)));
            }

            using var outputCanvas = _canvas.Clone(i => i.Resize(OutputSize, OutputSize));
            var outputPixels = MemoryMarshal.Cast<Gray8, byte>(outputCanvas.GetPixelSpan());
            var outputBytes = new byte[OutputSize * OutputSize];
            outputPixels.CopyTo(outputBytes);
            lock (_lock) _currentOutput = outputBytes;
            Task.Run(TryAnalyze);
            var source = BitmapSource.Create(OutputSize, OutputSize, 96, 96, PixelFormats.Gray8, null, _currentOutput, OutputSize);
            Image.Source = source;
        }

        private async Task TryAnalyze()
        {
            byte[] output = null;
            lock (_lock) {
                if (_analyzedOutput != null)
                    return;
                output = _analyzedOutput = _currentOutput;
            }
            Dispatcher.Invoke(() => {
                Analyzing.Text = "(analyzing)";
            });

            while (true) {
                var query = string.Join(" ", output.Select(i => (255-i).ToString()));
                var response = await _analyzer.Query(query);
                Dispatcher.Invoke(() => {
                    AnalyzerResponse.Text = response;
                });

                lock (_lock) {
                    if (_analyzedOutput == _currentOutput) {
                        _analyzedOutput = null;
                        break;
                    }
                    output = _analyzedOutput = _currentOutput;
                }
            }
            Dispatcher.Invoke(() => {
                Analyzing.Text = "";
            });
        }
    }
}
