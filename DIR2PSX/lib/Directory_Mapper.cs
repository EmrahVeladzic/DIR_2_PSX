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

    public static Executable? Program {get; set;}

    public static Int32 TotalSize {get; set;} = 0; 

    public static Int32 Level {get; set;} = 0; 

    public static Int32 LastIndex {get; set;} =0;

    public static List<GameDirectory> DirectoryTable {get; set;} = new();

    public static Int32 PathTableSize {get; set;} = 0;

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
            Console.WriteLine($"{gd.Name} - {gd.TableIndex}");
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


        ISO_Writer.CurrentIndex += ISO_Writer.SectorsNeeded(PathTableSize*2,false);
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
            asset.IsRaw=false;
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

        string? err = Validate();
        if(err!=null){return err;}

        AssignIndices();

        



        return null;

    }

}