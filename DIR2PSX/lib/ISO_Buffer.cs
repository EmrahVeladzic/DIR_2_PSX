namespace DIR2PSX;

public static class ISO_Buffer
{    
    public static readonly Int32 SECTOR_COUNT = 333000;
    public static readonly Int32 RAW_SECTOR_SIZE = 2352;
    public static readonly Int32 USER_SECTOR_SIZE = 2048;
    public static readonly Int32 VOLUME_SIZE = SECTOR_COUNT * RAW_SECTOR_SIZE;  
    public static readonly byte[] Buffer = new byte[VOLUME_SIZE];
    public static Span<byte> GetSector(Int32 index) => Buffer.AsSpan(index * RAW_SECTOR_SIZE, RAW_SECTOR_SIZE);

}

