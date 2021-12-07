using ForzaVinylPainting.Data;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Native;

namespace ForzaVinylPainting
{
    public class Simulator
    {
        const decimal SlowMovementStepSize = 0.5M;
        const decimal SlowScaleStepSize = 0.01M;
        const decimal SlowRotationStepSize = 0.1M;

        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly ScreenNumberExtractor _screenNumberExtractor = new();
        private readonly InputSimulator _simulator = new();
        private readonly Vector _currentPos = new();
        private readonly Vector _currentScale = new();
        private readonly Config _config;

        private decimal _currentRotation = 0M;
        private decimal _currentTransparency = 100M;
        private float _currentColorH = 999, _currentColorS = 999, _currentColorV = 999;
        private bool _nonUniformScalingEnabled = false;

        public Simulator(int screenWidth, int screenHeight, Config config)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _config = config;
        }

        public async Task ProcessShape(Shape shape)
        {
            var x = Math.Round(shape.ShapeX * 2M, MidpointRounding.AwayFromZero) / 2;
            var y = Math.Round(shape.ShapeY * 2M, MidpointRounding.AwayFromZero) / 2;

            // Movement
            _simulator.Keyboard.KeyPress(VirtualKeyCode.VK_1);
            await Delay(60);
            if (!await AdjustPositionSmart(x, y))
            {
                // Skip shape entirely if we can't position it correctly
                return;
            }

            // Resize
            _simulator.Keyboard.KeyPress(VirtualKeyCode.VK_2);
            await Delay(60);
            if (!_nonUniformScalingEnabled)
            {
                _simulator.Keyboard.KeyPress(VirtualKeyCode.LSHIFT);
                _nonUniformScalingEnabled = true;
            }
            await Delay(30);
            var scaledXScale = (shape.ShapeXRadius * 2M) / 128M;
            var scaledYScale = shape.ShapeYRadius.HasValue ? (shape.ShapeYRadius * 2M) / 128M : null;
            await AdjustScaleSmart(scaledXScale, scaledYScale);

            // Rotate
            if (shape.ShapeAngle != null)
            {
                _simulator.Keyboard.KeyPress(VirtualKeyCode.VK_3);
                await Delay(60);
                await AdjustRotationSmart((decimal)shape.ShapeAngle);
            }

            //// Transparency
            //_simulator.Keyboard.KeyPress(VirtualKeyCode.VK_5);
            //await Delay(20);
            //await Transparency((shape.Color.A / 255M) * 100);



            ColorToHSV(shape.ShapeColor, out double hue, out double saturation, out double value);

            Console.WriteLine($"Color: {shape.ShapeColor.R}/{shape.ShapeColor.G}/{shape.ShapeColor.B}      HSL: {hue}/{saturation}/{value}");
            await ChangeColor((float)hue / 360f, (float)saturation, (float)value);
            await Delay(150);
            await Stamp();

            // Shitty, but this forces a collect else the OCR keeps building up memory
            GC.Collect();
        }


        private async Task ChangeColor(float hPerc, float sPerc, float vPerc)
        {
            if (_currentColorH == hPerc && _currentColorS == sPerc && _currentColorV == vPerc)
            {
                return;
            }
            _nonUniformScalingEnabled = false;

            _currentColorH = hPerc;
            _currentColorS = sPerc;
            _currentColorV = vPerc;

            Console.WriteLine($"Final color: {hPerc}/{sPerc}/{vPerc}");
            await MoveMouseToPositionThenClick(19.5f, 92.7f);
            await Delay(200);
            await MoveMouseToPositionThenClick(19.5f, 92.7f);
            await Delay(50);

            var leftSide = 5.1f;
            var rightSide = 19.4f;
            var diff = rightSide - leftSide;


            // H
            await MoveMouseToPositionThenClick(diff * hPerc + leftSide, 32.7f);
            //await Delay(20);

            // S
            await MoveMouseToPositionThenClick(diff * sPerc + leftSide, 41.4f);
            //await Delay(20);

            // V
            await MoveMouseToPositionThenClick(diff * vPerc + leftSide, 50f);
            //await Delay(20);

            await MoveMouseToPositionThenClick(90, 90);

            await MoveMouseToPositionThenClick(6.6f, 91.3f);
            await Delay(20);
            await MoveMouseToPositionThenClick(6.6f, 92.7f);

        }

        private async Task MoveMouseToPositionThenClick(float percX, float percY)
        {
            var positions = ToScreenValuesFromPercent(percX, percY);
            _simulator.Mouse.MoveMouseTo(positions.Item1, positions.Item2);
            await Delay(80);
            _simulator.Mouse.LeftButtonDown();
            await Delay(80);
            _simulator.Mouse.LeftButtonUp();
            await Delay(80);
        }

        Tuple<float, float> ToScreenValuesFromPercent(float percX, float percY)
        {
            return ToScreenValuesFromPixels(percX / 100f * _screenWidth, percY / 100f * _screenHeight);
        }

        Tuple<float, float> ToScreenValuesFromPixels(float pixelX, float pixelY)
        {
            var x = pixelX * 65535 / _screenWidth;
            var y = pixelY * 65535 / _screenHeight;
            return new Tuple<float, float>(x, y);
        }

        private async Task<bool> AdjustPositionSmart(decimal x, decimal y)
        {
            Console.WriteLine($"Moving fast to {x},{y}");
            if (!await UpdateCurrentPosition())
            {
                return false;
            }

            TravelsFast(x, y, _currentPos.X, _currentPos.Y, 0.108M);
            Console.WriteLine($"Finished fast moving to to {x},{y}");

            await UpdateCurrentPosition();

            while (_currentPos.X != x || _currentPos.Y != y)
            {
                Console.WriteLine("Checking positions");

                var errorDiffX = x - _currentPos.X;
                var errorDiffY = y - _currentPos.Y;
                Console.WriteLine($"Real position {_currentPos.X},{_currentPos.Y}, had error of {errorDiffX},{errorDiffY}");

                TravelSlowIteration(x, _currentPos.X, SlowMovementStepSize, MovementType.Position, Axis.Horizontal);
                TravelSlowIteration(y, _currentPos.Y, SlowMovementStepSize, MovementType.Position, Axis.Vertical);
                if (!await UpdateCurrentPosition())
                {
                    return false;
                }
            }
            return true;
        }

        private async Task<bool> AdjustScaleSmart(decimal? x, decimal? y)
        {
            if (x.HasValue)
            {
                x = Math.Round(x.Value, 2);
            }

            if (y.HasValue)
            {
                y = Math.Round(y.Value, 2);
            }

            for (int i = 0; i < 2; i++)
            {
                Console.WriteLine($"Scaling fast to {x},{y}");
                if (!await UpdateCurrentScale())
                {
                    return false;
                }

                TravelsFast(x, null, _currentScale.X, _currentScale.Y, 0.00144M);
                TravelsFast(null, y, _currentScale.X, _currentScale.Y, 0.00144M);
            }
            Console.WriteLine($"Finished fast scaling to to {x},{y}");

            await UpdateCurrentScale();

            while ((x.HasValue && _currentScale.X != x) || (y.HasValue && _currentScale.Y != y))
            {
                Console.WriteLine("Checking positions");

                var errorDiffX = x - _currentScale.X;
                var errorDiffY = y - _currentScale.Y;
                Console.WriteLine($"Real scale {_currentScale.X},{_currentScale.Y}, had error of {errorDiffX},{errorDiffY}");

                TravelSlowIteration(x, _currentScale.X, SlowScaleStepSize, MovementType.Scale, Axis.Horizontal);
                TravelSlowIteration(y, _currentScale.Y, SlowScaleStepSize, MovementType.Scale, Axis.Vertical);
                if (!await UpdateCurrentScale())
                {
                    return false;
                }
            }
            return true;
        }

        private async Task<bool> AdjustRotationSmart(decimal rotation)
        {
            rotation = Math.Round(rotation, 1);

            Console.WriteLine($"Rotating fast to {rotation}");
            if (!await UpdateCurrentRotation())
            {
                return false;
            }

            TravelsFast(rotation, null, _currentRotation, 0, 0.073M, GetRotationAngleDiff(rotation), null);
            Console.WriteLine($"Finished fast rotating to {rotation}");

            await UpdateCurrentRotation();

            if (rotation == 0)
            {
                rotation = 360;
            }

            while (_currentRotation != rotation)
            {
                Console.WriteLine("Checking rotation");

                var errorDiff = rotation - _currentRotation;
                Console.WriteLine($"Real rotation {_currentRotation}, had error of {errorDiff}");

                TravelSlowIteration(rotation, _currentRotation, SlowRotationStepSize, MovementType.Rotation, Axis.Horizontal, GetRotationAngleDiff(rotation));
                if (!await UpdateCurrentRotation())
                {
                    return false;
                }
            }
            return true;
        }

        private decimal GetRotationAngleDiff(decimal targetRotation)
        {
            return Math.Round((_currentRotation - targetRotation + 540) % 360 - 180, 1);
        }

        private async Task<bool> UpdateCurrentPosition()
        {
            var positions = await _screenNumberExtractor.GetBoth();
            if (positions != null)
            {
                _currentPos.X = positions.Item1;
                _currentPos.Y = positions.Item2;
                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to get positions, ignoring");
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }
        }

        private async Task<bool> UpdateCurrentScale()
        {
            var scale = await _screenNumberExtractor.GetBoth();
            if (scale != null)
            {
                _currentScale.X = scale.Item1;
                _currentScale.Y = scale.Item2;
                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to get scale, ignoring");
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }
        }

        private async Task<bool> UpdateCurrentRotation()
        {
            var rotation = await _screenNumberExtractor.GetSingle();
            if (rotation.HasValue)
            {
                _currentRotation = rotation.Value;
                _currentRotation = Math.Round(_currentRotation, 1);
                if (_currentRotation == 0)
                {
                    _currentRotation = 360;
                }
                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to get rotation, ignoring");
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }
        }

        private async Task Stamp()
        {
            await MoveMouseToPositionThenClick(25.9f, 92.7f);
        }

        private void TravelsFast(decimal? x, decimal? y, decimal currentX, decimal currentY, decimal unitsPerMs, decimal? overrideAmountToMoveX = null, decimal? overrideAmountToMoveY = null)
        {
            var xKey = VirtualKeyCode.RETURN;
            var yKey = VirtualKeyCode.RETURN;
            var doX = false;
            var doY = false;
            var timeToTravelX = 0M;
            var timeToTravelY = 0M;

            if (x.HasValue)
            {
                var amountToMoveX = overrideAmountToMoveX.HasValue ? overrideAmountToMoveX.Value : x.Value - currentX;
                if (amountToMoveX != 0)
                {
                    if (amountToMoveX > 0)
                    {
                        xKey = VirtualKeyCode.VK_D;
                    }
                    else
                    {
                        xKey = VirtualKeyCode.VK_A;
                    }
                    doX = true;
                    timeToTravelX = Math.Abs(amountToMoveX) / unitsPerMs;
                    _simulator.Keyboard.KeyDown(xKey);
                }
            }

            if (y.HasValue)
            {
                var amountToMoveY = overrideAmountToMoveY.HasValue ? overrideAmountToMoveY.Value : y.Value - currentY;
                if (amountToMoveY != 0)
                {
                    if (amountToMoveY > 0)
                    {
                        yKey = VirtualKeyCode.VK_W;
                    }
                    else
                    {
                        yKey = VirtualKeyCode.VK_S;
                    }
                    doY = true;
                    timeToTravelY = Math.Abs(amountToMoveY) / unitsPerMs;
                    _simulator.Keyboard.KeyDown(yKey);
                }
            }

            if (!doX && !doY)
            {
                return;
            }

            Stopwatch sw = new();
            sw.Start();
            var finishedX = !doX;
            var finishedY = !doY;
            Console.WriteLine($"Moving fast for {timeToTravelX}ms, {timeToTravelY}ms");
            while (true)
            {
                var elapsed = sw.ElapsedMilliseconds;
                if (!finishedX && elapsed >= timeToTravelX)
                {
                    _simulator.Keyboard.KeyUp(xKey);
                    finishedX = true;
                }

                if (!finishedY && elapsed >= timeToTravelY)
                {
                    _simulator.Keyboard.KeyUp(yKey);
                    finishedY = true;
                }

                if (finishedX && finishedY)
                {
                    break;
                }
            }
        }

        private void TravelSlowIteration(decimal? targetValue, decimal currentValue, decimal movementStepSize, MovementType type, Axis axis, decimal? overrideAmountToMove = null)
        {
            Console.WriteLine($"Moving slow to {targetValue} from {currentValue}");

            if (targetValue.HasValue)
            {
                var amountToMove = overrideAmountToMove.HasValue ? overrideAmountToMove.Value : targetValue.Value - currentValue;
                if (amountToMove != 0)
                {
                    VirtualKeyCode key;
                    if (amountToMove > 0)
                    {
                        key = axis == Axis.Vertical ? VirtualKeyCode.UP : VirtualKeyCode.RIGHT;
                    }
                    else
                    {
                        key = axis == Axis.Vertical ? VirtualKeyCode.DOWN : VirtualKeyCode.LEFT;
                    }

                    Stopwatch sw = new();
                    sw.Start();

                    var time = 20;
                    var maxTicks = 10;

                    var amountMoved = 0M;
                    var amountToMoveAbs = Math.Abs(amountToMove);

                    for (int i = 0; i < maxTicks; i++)
                    {
                        _simulator.Keyboard.KeyDown(key);
                        _simulator.Keyboard.Sleep(time);
                        _simulator.Keyboard.KeyUp(key);
                        _simulator.Keyboard.Sleep(time);

                        amountMoved += movementStepSize;
                        if (amountMoved == amountToMoveAbs)
                        {
                            break;
                        }
                    }

                    Console.WriteLine("Finished slow moving");
                    return;
                }
            }

            Console.WriteLine("Skipping move");
        }


        private async Task Transparency(decimal percent)
        {
            var transparencyDiff = percent - _currentTransparency;
            if (transparencyDiff == 0)
            {
                return;
            }
            _currentTransparency = Math.Clamp(percent, 0, 100);

            var key = transparencyDiff > 0 ? VirtualKeyCode.VK_D : VirtualKeyCode.VK_A;
            transparencyDiff = Math.Abs(transparencyDiff);

            var unitsPerMs = 0.205M;
            var timeToTravel = transparencyDiff / unitsPerMs;

            _simulator.Keyboard.KeyDown(key);

            Stopwatch sw = new();
            sw.Start();
            while (true)
            {
                if (sw.ElapsedMilliseconds >= timeToTravel)
                {
                    break;
                }
            }
            _simulator.Keyboard.KeyUp(key);
            Console.WriteLine($"Changed transparency to {percent}");
        }

        private Task Delay(int ms)
        {
            return Task.Delay((int)(ms * _config.DelayMultiplier));
        }

        // https://stackoverflow.com/questions/359612/how-to-convert-rgb-color-to-hsv
        static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0 : 1d - (1d * min / max);
            value = max / 255d;
        }
    }
}
