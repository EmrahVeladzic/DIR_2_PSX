namespace DIR2PSX;

public static class ISO_Buffer
{    
    public static readonly Int32 SECTOR_COUNT = 333000;
    public static readonly Int32 RAW_SECTOR_SIZE = 2352;
    public static readonly Int32 USER_SECTOR_SIZE = 2048;
    public static readonly Int32 FMV_SECTOR_SIZE = 2324;
    public static readonly Int32 VOLUME_SIZE = SECTOR_COUNT * RAW_SECTOR_SIZE;  
    public static readonly byte[] Buffer = new byte[VOLUME_SIZE];

    public static readonly byte[] Sync = new byte[12]
    {
        0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00
    };

    public static readonly byte[] ECC_F_LUT;
    public static readonly byte[] ECC_B_LUT;

    static ISO_Buffer()
    {
        ECC_F_LUT = new byte[256];
        ECC_B_LUT = new byte[256];

        for (int i = 0; i < 256; i++)
        {
            int j = (i << 1) ^ (((i & 0x80) != 0) ? 0x11D : 0);
            ECC_F_LUT[i] = (byte)j;
            ECC_B_LUT[i ^ (byte)j] = (byte)i;
        }
    }

    public static readonly UInt32[] EDC_TABLE = Utils.BuildEDCTable();
    
    public static Span<byte> GetSector(Int32 index) => Buffer.AsSpan(index * RAW_SECTOR_SIZE, RAW_SECTOR_SIZE);



}