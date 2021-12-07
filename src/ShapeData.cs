using Newtonsoft.Json;
using System.Drawing;
using System.IO;

namespace ForzaVinylPainting
{
    public class ShapeData
    {
        public Shape[] shapes { get; set; }

        public static ShapeData FromFile(string path)
        {
            var input = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<ShapeData>(input);
        }
    }

    public class Shape
    {
        private int[] _color;
        private int[] _data;
        private int _type;

        public int Type
        {
            get => _type;
            set
            {
                _type = value;
                switch (_type)
                {
                    case 32:
                        ShapeType = ShapeType.Circle;
                        break;
                    case 8:
                        ShapeType = ShapeType.Ellipse;
                        break;
                    case 16:
                        ShapeType = ShapeType.RotatedEllipse;
                        break;
                    default:
                        ShapeType = ShapeType.Unknown;
                        break;
                }
            }
        }

        public int[] Data
        {
            get => _data;
            set
            {
                _data = value;
                ShapeX = Data[0];
                ShapeY = Data[1];
                ShapeXRadius = Data[2];

                if (Data.Length > 3)
                {
                    ShapeYRadius = Data[3];
                }

                if (Data.Length > 4)
                {
                    ShapeAngle = Data[4];
                }
            }
        }

        public int[] Color
        {
            get => _color;
            set
            {
                _color = value;
                ShapeColor = System.Drawing.Color.FromArgb(_color[3], _color[0], _color[1], _color[2]);
            }
        }

        public int ShapeX { get; private set; }
        public int ShapeY { get; private set; }
        public int ShapeXRadius { get; private set; }
        public int? ShapeYRadius { get; private set; }
        public int? ShapeAngle { get; private set; }
        public Color ShapeColor { get; private set; }
        public ShapeType ShapeType { get; private set; }
    }

    public enum ShapeType
    {
        Unknown,
        Circle,
        Ellipse,
        RotatedEllipse
    }
}
