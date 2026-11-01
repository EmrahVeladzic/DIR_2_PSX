namespace DIR2PSX;

public class DirectoryMapResult
{
    public GameDirectory? Data {get; set;} = null;
    public string? Error {get; set;} = null;
}

public class FileEntryMapResult
{
    public IFileEntry? Data {get; set;} = null;
    public string? Error {get; set;} = null;
}

public static class Directory_Mapper
{    
    public static GameDirectory? Root {get; set;}

    public static AssetFile? CNF {get; set;}

    public static Executable? Program {get; set;}

    public static Int32 TotalSize {get; set;} = 0; 

    public static Int32 Level {get; set;} = 0; 

    public static Int32 LastIndex {get; set;} =0;

    public static List<GameDirectory> DirectoryTable {get; set;} = new();

    public static Int32 PathTableSize {get; set;} = 0;

    public static Int32 FileStartingSector {get; set;} = 0;

    public static string CNFPath {get; set;} ="";

    public static string? Validate()
    {
        if (Root == null)
        {
            return "No root directory found.";
        }
        if (Root.Parent != null)
        {
            return "Root directory should not have a parent at this point.";
        }
        if (Program == null)
        {
            return "No executable found.";
        }
        if (CNF == null)
        {
            return "Failed to create SYSTEM.CNF.";
        }
        if (!Root.Files.Any(f => ReferenceEquals(f, CNF)))
        {
            return "SYSTEM.CNF not assigned to root.";
        }
        if (TotalSize > ISO_Buffer.VOLUME_SIZE)
        {
            return $"Exceeded maximum volume size ({TotalSize}/{ISO_Buffer.VOLUME_SIZE}).";
        }
        if (DirectoryTable.Count(d => d.Parent == null) > 1)
        {
            return "Cannot have multiple roots.";
        }
        
        return Root.Resolve();
    }

    public static void SetIndex(GameDirectory gd)
    {
        if (gd.TableIndex == 0)
        {
            LastIndex++;
            gd.TableIndex=LastIndex;
        }
        else
        {
            foreach(GameDirectory dir in gd.Subdirectories)
            {
                SetIndex(dir);
            }
        }
    }


    public static void AssignIndices()
    {
        if(Root==null){throw new Exception();}

        foreach(GameDirectory gd in DirectoryTable)
        {
            gd.TableIndex=0;
        }

        LastIndex=0;
        while (DirectoryTable.Any(d => d.TableIndex == 0))
        {
            SetIndex(Root);
        }

        DirectoryTable=DirectoryTable.OrderBy(d=>d.TableIndex).ToList();
    }

    public static string? SolveDirectoryRecords()
    {             

        foreach(GameDirectory gd in DirectoryTable)
        {   
            gd.LBA=ISO_Writer.CurrentIndex;

            gd.RecordBytes=68;


            foreach(GameDirectory subdir in gd.Subdirectories)
            {
                Int32 record_size = 33;
                record_size+=subdir.Name.Length;
                if ((subdir.Name.Length & 0x1) != 1)
                {
                    record_size++;
                }

                gd.RecordBytes += record_size;
            }

            foreach(IFileEntry f in gd.Files)
            {
                Int32 record_size = 33;
                record_size+=f.Name.Length;
                if ((f.Name.Length & 0x1) != 1)
                {
                    record_size++;
                }

                gd.RecordBytes += record_size;
            }

            if (gd.RecordBytes > ISO_Buffer.USER_SECTOR_SIZE)
            {
                return $"{((gd==Root)? "ROOT" : gd.Name)} - Directory record overrun ({gd.RecordBytes}/{ISO_Buffer.USER_SECTOR_SIZE}).";
            }

            ISO_Writer.CurrentIndex += ISO_Writer.SectorsNeeded(gd.RecordBytes,false);
            FileStartingSector=ISO_Writer.CurrentIndex;
        }        



        return null;
    }

    public static void GetPathTableBytes()
    {   
        PathTableSize=0;     

        foreach(GameDirectory gd in DirectoryTable)
        {
            Int32 record_size = 8;
            record_size+=gd.Name.Length;
            if ((gd.Name.Length & 0x1) == 1)
            {
                record_size++;
            }

            PathTableSize += record_size;

        }        


        ISO_Writer.CurrentIndex += ISO_Writer.SectorsNeeded(PathTableSize,false) * 2;
    }


    public static FileEntryMapResult MapFile(string path,string name)
    {
        FileEntryMapResult output = new();

        string current = !string.IsNullOrWhiteSpace(name)? Path.Combine(path, name) : path;

        if (!File.Exists(current))
        {
            output.Error=$"{name} - File does not exist.";
            return output;
        }

        FileInfo info = new FileInfo(current);

        if (info.Length >= ISO_Buffer.VOLUME_SIZE - TotalSize)
        {
            output.Error = "File exceeds maximum remaining space size.";
            return output;
        }

        

        Int32 file_size = (Int32)info.Length;
        TotalSize+=file_size;

        IFileEntry? file = null;
        


        if (info.Extension.ToUpperInvariant() == ".ELF")
        {
            if (Program != null)
            {
                output.Error = $"{name} - Multiple ELF files found.";
                return output;
            }

            CNFPath = path+"/";

            CNF = new AssetFile{Data= new byte[ISO_Buffer.USER_SECTOR_SIZE],Name="SYSTEM.CNF"};


            Executable exe = new();
            exe.Name=name;            
            exe.Size=file_size;
            exe.Data = File.ReadAllBytes(current);
            Program = exe;
            file=exe;
        }
        else if (info.Extension.ToUpperInvariant() == ".EXE")
        {
            output.Error = "You may not use pre-converted .EXE files.";
            return output;
        }
        else
        {
            AssetFile asset = new();
            asset.Name=name;
            asset.Size=file_size;
            asset.Data = File.ReadAllBytes(current);
            file=asset;
        }

        output.Data = file;
        
        return output;
     
    }

    public static DirectoryMapResult MapDirectory(string path,string name,GameDirectory? parent)
    {
        DirectoryMapResult output = new();

        Level++;
        if (Level > 8)
        {
            output.Error="Exceeded the 8th level of subdirectories.";
            return output;
        }

        string current = !string.IsNullOrWhiteSpace(name)? Path.Combine(path, name) : path;

        if (!Directory.Exists(current))
        {
            output.Error=$"{name} - Directory does not exist.";
            return output;
        }

        GameDirectory directory = new();
        directory.Name=name;

        DirectoryInfo info = new DirectoryInfo(current);

        FileInfo[] files = info.GetFiles();
        DirectoryInfo[] subdirectories = info.GetDirectories();
        
        for(int i = 0; i<files.Length; i++)
        {
            FileEntryMapResult file = MapFile(current,files[i].Name);
            if(file.Error!=null||file.Data==null){output.Error=file.Error; return output;}
            else{directory.Files.Add(file.Data);}
        }
        for(int i = 0; i<subdirectories.Length; i++)
        {
            DirectoryMapResult subdirectory = MapDirectory(current,subdirectories[i].Name,directory);
            if(subdirectory.Error!=null||subdirectory.Data==null){output.Error=subdirectory.Error; return output;}
            else{directory.Subdirectories.Add(subdirectory.Data);}
        }

        directory.Subdirectories=directory.Subdirectories.OrderBy(d=>d.Name).ToList();
        directory.Parent=parent;
        DirectoryTable.Add(directory);

        Level--;
        output.Data=directory;
        return output;
    }

    public static string? Traverse(string path,string name)
    {
        DirectoryMapResult root = MapDirectory(path,name,null);

        if ((root.Error == null && root.Data == null)||(root.Error!=null && root.Data!=null))
        {
            throw new Exception();
        }

        Root=root.Data;

        if (root.Error != null)
        {
            return root.Error;
        }

        string rl = !string.IsNullOrWhiteSpace(name)? Path.Combine(path, name) : path;

        if (Root != null && CNF != null)
        {
            Root.Files.Add(CNF);
        }


        CNFPath=CNFPath.Replace(rl,"cdrom:");
        CNFPath=CNFPath.Replace("/","\\");
        CNFPath=CNFPath.Replace("\\\\","\\");


        string? err = Validate();

        if(err!=null){return err;}

        if(CNF==null){throw new Exception();}
        string cnf_content = $"BOOT={CNFPath}\r\nTCB=4\r\nEVENT=10\r\nSTACK={EXE_Creator.Stack}\r\n";
        cnf_content=cnf_content.Replace("CDROM","cdrom");
        CNF.Data= new byte[cnf_content.Length];
        CNF.Size=CNF.Data.Length;
        Utils.WriteString(CNF.Data,0,cnf_content.Length,cnf_content);
        



        AssignIndices();

        GetPathTableBytes();

        err = SolveDirectoryRecords();

        if(err != null)
        {
            return err;
        }

        return null;

    }

}