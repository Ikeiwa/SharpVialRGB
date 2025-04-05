using System.Collections;
using System.Text;
using HidApi;

namespace SharpVialRGB;

internal static class VialConstants
{
    public static uint MSG_LEN = 32;

    public static string VIAL_SERIAL_NUMBER_MAGIC = "vial:f64c2b3c";

    public static byte VIALRGB_EFFECT_DIRECT = 1;

    public static byte CMD_VIA_DYNAMIC_KEYMAP_GET_KEYCODE = 0x04;
    public static byte CMD_VIA_LIGHTING_SET_VALUE = 0x07;
    public static byte CMD_VIA_LIGHTING_GET_VALUE = 0x08;

    public static byte VIALRGB_GET_INFO = 0x40;
    public static byte VIALRGB_GET_MODE = 0x41;
    public static byte VIALRGB_GET_SUPPORTED = 0x42;
    public static byte VIALRGB_GET_NUMBER_LEDS = 0x43;
    public static byte VIALRGB_GET_LED_INFO = 0x44;

    public static byte VIALRGB_SET_MODE = 0x41;
    public static byte VIALRGB_DIRECT_FASTSET = 0x42;
}

public class VialRgbLed()
{
    internal uint idx;
    internal byte x;
    internal byte y;
    internal byte flags;
    internal byte row;
    internal byte col;
    internal QmkKeycode keycode;
    internal bool needUpdate = false;
    internal byte h;
    internal byte s;
    internal byte v;

    public override string ToString()
    {
        return $"VialRGBLed(idx={idx}, x={x}, y={y}, flags={flags}, row={row}, col={col})";
    }

    public uint Id => idx;
    public byte X => x;
    public byte Y => y;
    public byte Row => row;
    public byte Col => col;

    public QmkKeycode Keycode => keycode;

    public byte Hue
    {
        get => h;
        set
        {
            needUpdate |= h != value;
            h = value;
        }
    }

    public byte Saturation
    {
        get => s;
        set
        {
            needUpdate |= s != value;
            s = value;
        }
    }

    public byte Value
    {
        get => v;
        set
        {
            needUpdate |= v != value;
            v = value;
        }
    }
}

public static class VialRGB
{
    public struct ByteMsg : IEnumerable<byte>, IEquatable<ByteMsg>
    {
        private List<byte> _bytes;

        public int Length => _bytes.Count;

        public ByteMsg(List<byte> bytes)
        {
            _bytes = new List<byte>(bytes);
        }

        public ByteMsg(byte[] bytes)
        {
            _bytes = new List<byte>(bytes);
        }

        public ByteMsg(byte a)
        {
            _bytes = new List<byte>() { a };
        }

        public ByteMsg(string byteStr)
        {
            _bytes = Encoding.ASCII.GetBytes(byteStr).ToList();
        }

        public ByteMsg()
        {
            _bytes = new List<byte>();
        }

        public ByteMsg Add(ByteMsg b)
        {
            _bytes.AddRange(b._bytes);
            return this;
        }

        public ByteMsg Add(byte b)
        {
            _bytes.Add(b);
            return this;
        }

        public ByteMsg Push(ByteMsg b)
        {
            _bytes.InsertRange(0, b._bytes);
            return this;
        }

        public ByteMsg Push(byte b)
        {
            _bytes.Insert(0, b);
            return this;
        }

        public static ByteMsg operator +(ByteMsg a, ByteMsg b)
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(a._bytes);
            bytes.AddRange(b._bytes);

            return new ByteMsg(bytes);
        }
        
        public bool Equals(ByteMsg other)
        {
            return _bytes.Equals(other._bytes);
        }

        public override bool Equals(object? obj)
        {
            return obj is ByteMsg other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _bytes.GetHashCode();
        }

        public static bool operator ==(ByteMsg a, ByteMsg b)
        {
            if (a._bytes.Count != b._bytes.Count)
                return false;

            for (int i = 0; i < a._bytes.Count; i++)
            {
                if (a._bytes[i] != b._bytes[i])
                    return false;
            }

            return true;
        }

        public static bool operator !=(ByteMsg a, ByteMsg b)
        {
            return !(a == b);
        }

        public static bool operator ==(ByteMsg a, byte[] b)
        {
            if (a._bytes.Count != b.Length)
                return false;

            for (int i = 0; i < a._bytes.Count; i++)
            {
                if (a._bytes[i] != b[i])
                    return false;
            }

            return true;
        }

        public static bool operator !=(ByteMsg a, byte[] b)
        {
            return !(a == b);
        }

        public byte this[int key]
        {
            get => _bytes[key];
            set => _bytes[key] = value;
        }

        public static implicit operator bool(ByteMsg exists)
        {
            return exists == default(ByteMsg);
        }

        public static implicit operator ByteMsg(List<byte> bytes)
        {
            return new ByteMsg(bytes);
        }

        public static implicit operator List<byte>(ByteMsg bytes)
        {
            return bytes._bytes;
        }

        public static implicit operator byte[](ByteMsg bytes)
        {
            return bytes._bytes.ToArray();
        }

        public static implicit operator ByteMsg(byte[] bytes)
        {
            return new ByteMsg(bytes);
        }

        public static implicit operator ReadOnlySpan<byte>(ByteMsg bytes)
        {
            return bytes._bytes.ToArray();
        }

        public static implicit operator ByteMsg(byte value)
        {
            return new ByteMsg(value);
        }

        public ByteMsg Get(int start, int end)
        {
            if (start < 0 || start >= _bytes.Count)
                throw new IndexOutOfRangeException();

            if (end < start || end > _bytes.Count)
                throw new IndexOutOfRangeException();

            return _bytes.GetRange(start, end - start);
        }

        public ByteMsg GetRange(int start, int count)
        {
            if (start < 0 || start >= _bytes.Count)
                throw new IndexOutOfRangeException();

            if (count < 1 || start + count >= _bytes.Count)
                throw new IndexOutOfRangeException();

            return _bytes.GetRange(start, count);
        }

        public ByteMsg GetOffseted(int start)
        {
            if (start < 0 || start >= _bytes.Count)
                throw new IndexOutOfRangeException();

            return _bytes.GetRange(start, _bytes.Count - start);
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return _bytes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _bytes.GetEnumerator();
        }

        public override string ToString()
        {
            return BitConverter.ToString(_bytes.ToArray()).Replace("-"," ");
        }
    }

    private static ushort GetUShortFromBytes(byte[] buffer, bool littleEndian = false)
    {
        if (littleEndian)
            return StructPacker.UnpackSingle<ushort>("<H", buffer);

        return StructPacker.UnpackSingle<ushort>(">H", buffer);
    }

    private static ByteMsg HidSend(Device dev, ByteMsg msg, uint retries = 1, bool waitForResponce = true)
    {
        if (msg.Length > VialConstants.MSG_LEN)
            throw new ApplicationException("message must be less than 32 bytes");

        uint targetLength = VialConstants.MSG_LEN - (uint)msg.Length;
        for (int i = 0; i < targetLength; i++)
            msg.Add(0x00);

        msg.Push(0x00);

        byte[] data = new byte[VialConstants.MSG_LEN];
        bool first = true;

        while (retries > 0)
        {
            retries--;

            if (!first)
                Thread.Sleep(50);
            first = false;

            try
            {
                dev.Write(msg);

                if (waitForResponce)
                {
                    int readBytes = dev.Read(data);
                    if (readBytes == 0)
                        continue;
                }
            }
            catch (Exception)
            {
                continue;
            }

            break;
        }

        if (data == null)
            throw new ApplicationException("failed to communicate with the device");
        return data;
    }

    private static bool IsRawHid(DeviceInfo desc)
    {
        if (desc.UsagePage != 0xFF60 || desc.Usage != 0x61)
            return false;

        Device dev;

        try
        {
            dev = desc.ConnectToDevice();
        }
        catch (Exception)
        {
            return false;
        }

        ByteMsg data = new ByteMsg();
        try
        {
            data = HidSend(dev, 0x01, 3);
        }
        catch (Exception)
        {
            // ignored
        }

        dev.Dispose();

        if (data.Length >= 3 && data.Get(0, 3) != new byte[] { 0x01, 0x00, 0x09 })
            return false;

        return true;
    }

    private static bool IsVialRgb(DeviceInfo desc)
    {
        Device dev;

        try
        {
            dev = desc.ConnectToDevice();
        }
        catch (Exception)
        {
            return false;
        }

        ByteMsg data = new ByteMsg();
        try
        {
            data = HidSend(dev, new byte[] { 0xfe, 0x00 }, 3);
        }
        catch (Exception)
        {
            // ignored
        }

        dev.Dispose();

        if (data.Length != VialConstants.MSG_LEN)
            return false;

        var vialProtocol = StructPacker.UnpackSingle<uint>("<I", data.Get(0, 13));

        if (vialProtocol < 4)
            return false;

        return true;
    }

    private static DeviceInfo[] FindVialDevices()
    {
        var devices = Hid.Enumerate();

        List<DeviceInfo> foundDevices = new List<DeviceInfo>();

        foreach (var deviceInfo in devices)
        {
            if (deviceInfo.SerialNumber.Contains(VialConstants.VIAL_SERIAL_NUMBER_MAGIC) && IsRawHid(deviceInfo) &&
                IsVialRgb(deviceInfo))
            {
                var device = deviceInfo.ConnectToDevice();
                if (VialRgbGetModes(device).Contains(VialConstants.VIALRGB_EFFECT_DIRECT))
                    foundDevices.Add(deviceInfo);
            }
        }

        return foundDevices.ToArray();
    }

    private static ushort[] VialRgbGetModes(Device dev)
    {
        var data = HidSend(dev, new[] { VialConstants.CMD_VIA_LIGHTING_GET_VALUE, VialConstants.VIALRGB_GET_INFO }, 20)
            .GetOffseted(2);

        var rgbVersion = data[0] | (data[1] << 8);
        if (rgbVersion != 1)
            throw new ApplicationException($"Unsupported VialRGB protocol ({rgbVersion})");

        var rgbSupportedEffects = new List<ushort>();
        uint maxEffects = 0;

        while (maxEffects < 0xFFFF)
        {
            data = HidSend(dev,
                StructPacker.Pack("<BBH", VialConstants.CMD_VIA_LIGHTING_GET_VALUE, VialConstants.VIALRGB_GET_SUPPORTED,
                    maxEffects)).GetOffseted(2);

            for (int i = 0; i < data.Length - 1; i += 2)
            {
                var value = GetUShortFromBytes(data.Get(i, i + 2), true);
                if (value != 0xFFFF)
                    rgbSupportedEffects.Add(value);
                maxEffects = Math.Max(maxEffects, value);
            }
        }

        return rgbSupportedEffects.ToArray();
    }

    internal static VialRgbLed[] VialRgbGetLeds(Device dev)
    {
        var data = HidSend(dev, new[] { VialConstants.CMD_VIA_LIGHTING_GET_VALUE, VialConstants.VIALRGB_GET_NUMBER_LEDS });
        int numLeds = StructPacker.UnpackSingle<ushort>("<H", data.Get(2, 4));

        var leds = new List<VialRgbLed>();
        for (uint idx = 0; idx < numLeds; idx++)
        {
            data = HidSend(dev,
                StructPacker.Pack("<BBH", VialConstants.CMD_VIA_LIGHTING_GET_VALUE, VialConstants.VIALRGB_GET_LED_INFO, idx));
            var (x, y, flags, rowByte, colByte) =
                StructPacker.Unpack<(byte, byte, byte, byte, byte)>("BBBBB", data.Get(2, 7));

            byte? row = rowByte;
            if (rowByte == 0xFF)
                row = null;

            byte? col = colByte;
            if (colByte == 0xFF)
                col = null;

            ushort? keycode = null;

            if (row.HasValue && col.HasValue)
            {
                data = HidSend(dev,
                    new byte[] { VialConstants.CMD_VIA_DYNAMIC_KEYMAP_GET_KEYCODE, 0, row.Value, col.Value });
                keycode = GetUShortFromBytes(data.Get(4, 6));
            }

            row ??= 0;
            col ??= 0;
            keycode ??= 0;

            leds.Add(new VialRgbLed
                { idx = idx, x = x, y = y, flags = flags, row = row.Value, col = col.Value, keycode = (QmkKeycode)keycode.Value });
        }

        return leds.ToArray();
    }

    internal static void VialRgbSetMode(Device dev, byte mode)
    {
        HidSend(dev,
            StructPacker.Pack("BBHBBBB", VialConstants.CMD_VIA_LIGHTING_SET_VALUE, VialConstants.VIALRGB_SET_MODE, mode, 128,
                128, 128, 128), 20);
    }

    internal static byte VialRgbGetMode(Device dev)
    {
        var data = HidSend(dev,
            new[] { VialConstants.CMD_VIA_LIGHTING_GET_VALUE, VialConstants.VIALRGB_GET_MODE }).GetOffseted(2);
        
        return (byte)GetUShortFromBytes(data.Get(0, 2), true);
    }
    
    internal static void VialRgbSendLeds(Device dev, VialRgbLed[] leds)
    {
        var sendPerPacket = 9;

        for (int x = 0; x < leds.Length; ++x)
        {
            if (x != leds[x].idx)
            {
                throw new ApplicationException("Leds got reordered");
            }
        }

        ushort numLeds = (ushort)leds.Length;

        for (ushort idx = 0; idx < numLeds; ++idx)
        {
            List<VialRgbLed> ledsToSend = new List<VialRgbLed>();
            
            while (idx < numLeds && !leds[idx].needUpdate)
                idx++;

            ushort startIndex = idx;
            
            if(idx >= numLeds)
                continue;

            do
            {
                ledsToSend.Add(leds[idx]);
                idx++;
            }while (idx < numLeds && ledsToSend.Count < sendPerPacket && leds[idx].needUpdate);
            
            idx--;
            
            ByteMsg buffer = new ByteMsg();
            foreach (var led in ledsToSend)
            {
                buffer.Add(new[] { led.h, led.s, led.v });
                led.needUpdate = false;
            }

            var payload = new ByteMsg(StructPacker.Pack("BBHB", VialConstants.CMD_VIA_LIGHTING_SET_VALUE,
                VialConstants.VIALRGB_DIRECT_FASTSET, startIndex, ledsToSend.Count));
            foreach (var x in buffer)
                payload.Add(StructPacker.Pack("<B", x));

            HidSend(dev, payload, waitForResponce:false);
        }
    }

    internal static void VialRgbSendAllLeds(Device dev, VialRgbLed[] leds)
    {
        var sendPerPacket = 9;

        for (int x = 0; x < leds.Length; ++x)
        {
            if (x != leds[x].idx)
            {
                throw new ApplicationException("Leds got reordered");
            }
        }

        ushort numLeds = (ushort)leds.Length;
        ushort sent = 0;

        while (sent < numLeds)
        {
            ushort startLed = sent;
            ByteMsg buffer = new ByteMsg();

            int actualTotalSend = sendPerPacket;
            if (startLed + sendPerPacket > numLeds)
                actualTotalSend = numLeds - startLed;

            List<VialRgbLed> ledsToSend = leds.ToList().GetRange(startLed, actualTotalSend);

            foreach (var led in ledsToSend)
            {
                buffer.Add(new[] { led.h, led.s, led.v });
                led.needUpdate = false;
            }

            var payload = new ByteMsg(StructPacker.Pack("BBHB", VialConstants.CMD_VIA_LIGHTING_SET_VALUE,
                VialConstants.VIALRGB_DIRECT_FASTSET, startLed, ledsToSend.Count));
            foreach (var x in buffer)
                payload.Add(StructPacker.Pack("<B", x));

            HidSend(dev, payload, waitForResponce:false);

            sent += (ushort)ledsToSend.Count;
        }
    }

    public static VialDevice[] GetAllDevices()
    {
        var hidDevices = FindVialDevices();

        var vialDevices = new VialDevice[hidDevices.Length];
        for (int i = 0; i < vialDevices.Length; ++i)
        {
            vialDevices[i] = new VialDevice(hidDevices[i]);
        }

        return vialDevices;
    }
}