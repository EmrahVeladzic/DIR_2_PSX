namespace DIR2PSX;

public static class ISO_Writer
{    
    public static Int32 CurrentIndex {get; set;} = 0;

    public static Int32 SectorsNeeded(Int32 byte_length, bool raw = false)
    {
        Int32 sector_size = raw ? ISO_Buffer.RAW_SECTOR_SIZE : ISO_Buffer.USER_SECTOR_SIZE;
        return (byte_length + sector_size - 1) / sector_size;
    }

    public static string? WriteData(byte[] data)
    {
        if (ISO_Buffer.SECTOR_COUNT-CurrentIndex<SectorsNeeded(data.Length,false))
        {
            return "Out of sectors to write to.";
        }


        return null;
    }

    public static string? WriteRaw(byte[] data)
    {
        if (ISO_Buffer.SECTOR_COUNT-CurrentIndex<SectorsNeeded(data.Length,true))
        {
            return "Out of sectors to write to.";
        }



        return null;
    }





}

