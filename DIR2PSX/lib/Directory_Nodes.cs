namespace DIR2PSX;

public static class CharsetValidator
{    
    public static readonly HashSet<char> AllowedChars = new()
    {
        'A','B','C','D','E','F','G','H','I','J','K','L','M',
        'N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
        '0','1','2','3','4','5','6','7','8','9','_'
    };
}

public interface IFileEntry
{
    public Int32 Size {get; set;}
    public string Name {get; set;}  
    public string? Resolve();
    public byte[] Data {get; set;}
    public Int32 LBA {get; set;}
}

public interface IDirectory
{
    public string Name {get; set;} 

    public List<IDirectory> Subdirectories {get; set;}

    public List<IFileEntry> Files {get; set;}

    public IDirectory? Parent {get; set;}

    public Int32 TableIndex {get; set;}

    public Int32 LBA {get; set;}

    public string? Resolve();


}

public class AssetFile : IFileEntry
{
    public Int32 Size {get; set;} = 0;
    public string Name {get; set;} = "";
    public bool IsRaw {get; set;} = false;
    public byte[] Data {get; set;} = [];
    public Int32 LBA {get; set;}

    public string? Resolve()
    {
        if (Name != Name.ToUpperInvariant())
        {
            return $"{Name} - File names and extensions must be uppercase.";
        }

        int dotCount = Name.Count(c => c == '.');

        if (dotCount != 1)
        {
            return $"{Name} - The file needs to have a single dot separator.";
        }

        string[] parts = Name.Split(".",2);

        if (string.IsNullOrWhiteSpace(parts[0]))
        {
            return $"{Name} - Empty names are not allowed.";
        }

        if (string.IsNullOrWhiteSpace(parts[1]))
        {
            return $"{Name} - Empty extensions are not allowed.";
        }

        if (parts[0].Length<1||parts[0].Length>8)
        {
            return $"{Name} - The name needs to be between 1 and 8 characters long.";
        }

        if (parts[1].Length<1||parts[1].Length>3)
        {
            return $"{Name} - The extension needs to be between 1 and 3 characters long.";
        }

        for(int i = 0; i<Name.Length; i++)
        {
            if(Name[i]!='.'&& !CharsetValidator.AllowedChars.Contains(Name[i]))
            {
                
                return $"{Name} - The allowed characters are \"A-Z\", \"0-9\", and \"_\".";

            }
        }      

        Name+=";1";  
        
        return null;
    }

}

public class Executable : IFileEntry
{
    public Int32 Size {get; set;} = 0;
    public string Name {get; set;} = "";       
    public byte[] Data {get; set;} = [];
    public Int32 LBA {get; set;}
    public string? Resolve()
    {
        string ResolvedName = "";

        int dotCount = Name.Count(c => c == '.');

        if (dotCount != 1)
        {
            return $"{Name} - The file needs to have a single dot separator.";
        }

        string[] parts = Name.Split(".",2);

        if(parts[1]=="EXE"){return null;}

        if (string.IsNullOrWhiteSpace(parts[0]))
        {
            return $"{Name} - Empty names are not allowed.";
        }

        if (parts[0].Length<1||parts[0].Length>8)
        {
            return $"{Name} - The name needs to be between 1 and 8 characters long.";
        }

        ResolvedName=parts[0].ToUpperInvariant()+".EXE";

        for(int i = 0; i<ResolvedName.Length; i++)
        {
            if(ResolvedName[i]!='.'&& !CharsetValidator.AllowedChars.Contains(ResolvedName[i]))
            {
                
                return $"{Name} - Could not resolve, got {ResolvedName} - The allowed characters are \"A-Z\", \"0-9\", and \"_\".";

            }
        }     

        Name=ResolvedName;   

        EXEResult result = EXE_Creator.GetEXE(Data,parts[0].ToUpperInvariant());

        if((result.Data==null&&result.Error==null)||(result.Data!=null&&result.Error!=null)){throw new Exception();}

        if (result.Error != null)
        {
            return result.Error;
        }
        if (result.Data != null)
        {
            Data=result.Data;
            Size = Data.Length;
        
        }
          
        Name+=";1"; 
        
        return null;
    }

}

public class GameDirectory : IDirectory
{
    public string Name {get; set;} = "";

    public List<IDirectory> Subdirectories {get; set;} = new();

    public List<IFileEntry> Files {get; set;} = new();

    public IDirectory? Parent {get; set;}

    public Int32 TableIndex {get; set;} = 0;

    public Int32 LBA {get; set;}

    public string? Resolve()
    {   
        if(Parent!=null){

            if (string.IsNullOrWhiteSpace(Name))
            {
                return $"{Name} - Empty names are not allowed.";
            }

            if (Name.Length < 1 || Name.Length > 8)
            {
                return $"{Name} - Directory names need to be between 1 and 8 characters long.";
            }

            if (Name != Name.ToUpper())
            {
                return $"{Name} - Directory names need to be uppercase.";
            }

            for(int i = 0; i<Name.Length; i++)
            {
                if(!CharsetValidator.AllowedChars.Contains(Name[i]))
                {                
                    return $"{Name} - The allowed characters are \"A-Z\", \"0-9\", and \"_\".";

                }
            }    

        }
        else{Name="\0";}
        
        foreach(IDirectory sub in Subdirectories)
        {
            string? cascade = sub.Resolve();

            if (cascade != null)
            {
                return Name+"/"+cascade;
            }
        }

        foreach(IFileEntry f in Files)
        {
            string? cascade = f.Resolve();

            if (cascade != null)
            {
                return Name+"/"+cascade;
            }
        }
        
        Files=Files.OrderBy(f=>f.Name).ToList();

        return null;
    }    

}


