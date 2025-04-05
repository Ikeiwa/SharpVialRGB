using HidApi;

namespace SharpVialRGB;

public class VialDevice
{
    private DeviceInfo _deviceInfo;
    private Device _device;
    private byte _originalMode;
    private VialRgbLed[] _leds;
    private VialRgbLed[,] _ledsMatrix;
    private VialRgbLed[,] _ledsMatrixColRow;
    private Dictionary<QmkKeycode, VialRgbLed> _ledsByKeycode;

    public int SizeX { get; private set; }
    public int SizeY { get; private set; }
    public int Columns { get; private set; }
    public int Rows { get; private set; }

    public VialRgbLed[] Leds => _leds;
    public string Name => _deviceInfo.ProductString;
    public string Manufacturer => _deviceInfo.ManufacturerString;
    public string Serial => _deviceInfo.SerialNumber;
    
    internal VialDevice(DeviceInfo deviceInfo)
    {
        _deviceInfo = deviceInfo;
        _device = null!;
        _leds = null!;
    }

    public void Connect()
    {
        _device = _deviceInfo.ConnectToDevice();
        _leds = VialRGB.VialRgbGetLeds(_device);
        _originalMode = VialRGB.VialRgbGetMode(_device);

        SizeX = 0;
        SizeY = 0;
        Columns = 0;
        Rows = 0;

        foreach (var led in _leds)
        {
            if(led.X > SizeX)
                SizeX = led.X;
            if(led.Y > SizeY)
                SizeY = led.Y;
            if(led.Col > Columns)
                Columns = led.Col;
            if(led.Row > Rows)
                Rows = led.Row;
        }

        SizeX++;
        SizeY++;
        Columns++;
        Rows++;

        _ledsMatrix = new VialRgbLed[SizeX, SizeY];
        _ledsMatrixColRow = new VialRgbLed[Columns, Rows];
        _ledsByKeycode = new Dictionary<QmkKeycode, VialRgbLed>();
        
        foreach (var led in _leds)
        {
            _ledsMatrix[led.X, led.Y] = led;
            _ledsMatrixColRow[led.Col, led.Row] = led;
            _ledsByKeycode.Add(led.keycode,led);
        }
    }

    public void Dispose()
    {
        VialRGB.VialRgbSetMode(_device, _originalMode);
        
        _device.Dispose();
    }

    public void EnableDirectRgb()
    {
        VialRGB.VialRgbSetMode(_device, VialConstants.VIALRGB_EFFECT_DIRECT);
    }

    public void Update(bool refreshAll = false)
    {
        if(refreshAll)
            VialRGB.VialRgbSendAllLeds(_device, _leds);
        else
            VialRGB.VialRgbSendLeds(_device, _leds);
    }

    public VialRgbLed GetLedAtCoords(byte x, byte y)
    {
        return _ledsMatrix[x, y];
    }

    public VialRgbLed GetLedByKeycode(QmkKeycode key)
    {
        return _ledsByKeycode.GetValueOrDefault(key);
    }
    
    public VialRgbLed GetLedByKeycode(StandardKeycode key)
    {
        return _ledsByKeycode.GetValueOrDefault(KeycodeUtils.GetQmkFromStandard(key));
    }

    public VialRgbLed GetLedByColumnRow(byte col, byte row)
    {
        return _ledsMatrixColRow[row, col];
    }
}