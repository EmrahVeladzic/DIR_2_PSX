using System.Text;

namespace DIR2PSX;

public static class Utils {
    public static void WriteString(byte[] buffer, Int32 offset, Int32 length, string value)
    {
        string padded = value.ToUpperInvariant().PadRight(length, ' ');
        byte[] bytes = Encoding.ASCII.GetBytes(padded.Substring(0, length));
        Array.Copy(bytes, 0, buffer, offset, length);
    }

    public static void WriteInt16BothEndian(byte[] buffer, Int32 offset, UInt16 value)
    {
        buffer[offset + 0] = (byte)(value & 0xFF);         
        buffer[offset + 1] = (byte)(value >> 8);           
        buffer[offset + 2] = (byte)(value >> 8);           
        buffer[offset + 3] = (byte)(value & 0xFF);         
    }

    public static void WriteInt32BothEndian(byte[] buffer, Int32 offset, UInt32 value)
    {
        buffer[offset + 0] = (byte)(value & 0xFF);         
        buffer[offset + 1] = (byte)(value >> 8 & 0xFF);    
        buffer[offset + 2] = (byte)(value >> 16 & 0xFF);   
        buffer[offset + 3] = (byte)(value >> 24);          
        buffer[offset + 4] = (byte)(value >> 24);          
        buffer[offset + 5] = (byte)(value >> 16 & 0xFF);   
        buffer[offset + 6] = (byte)(value >> 8 & 0xFF);    
        buffer[offset + 7] = (byte)(value & 0xFF);         
    }

    public static string ReadString(byte[] data, int offset, int length)
    {
        return System.Text.Encoding.ASCII.GetString(data, offset, length);
    }

}