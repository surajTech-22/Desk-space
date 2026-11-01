using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DeskSpace;

// R252: backup integrity receipt created BEFORE changing an Excel destination.
// No workbook content is parsed, changed or opened in Office by this class.
internal static class DeskSpaceExcelBackupReceiptV252
{
    private const long MaxBackupBytes=2L*1024*1024*1024;

    private static string Hash(string file)
    {
        var info=new FileInfo(file);
        if(!info.Exists || info.Length<=0 || info.Length>MaxBackupBytes ||
           (info.Attributes & FileAttributes.ReparsePoint)!=0)
            throw new InvalidDataException(
                "The Excel safety copy is missing, linked or has invalid size.");
        long length=info.Length;
        using var stream=new FileStream(info.FullName,FileMode.Open,
            FileAccess.Read,FileShare.Read,64*1024,FileOptions.SequentialScan);
        string sha=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        info.Refresh();
        if(!info.Exists || info.Length!=length)
            throw new IOException("The Excel safety copy changed during hashing.");
        return sha;
    }

    internal static string Create(string backupPath)
    {
        string backup=Path.GetFullPath(backupPath);
        string hash=Hash(backup);
        string receipt=backup+".sha256.txt";
        var temp=receipt+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            string text=hash+"  "+Path.GetFileName(backup)+"\n";
            File.WriteAllText(temp,text,new UTF8Encoding(false));
            // Never replace an existing record. A collision must block the
            // format transaction rather than silently invalidate old proof.
            File.Move(temp,receipt,false);
            if(!Verify(backup,receipt))
                throw new InvalidDataException(
                    "The Excel pre-change safety copy did not verify.");
            return receipt;
        }
        finally
        {
            if(File.Exists(temp))File.Delete(temp);
        }
    }

    internal static bool Verify(string backupPath,string receiptPath)
    {
        try
        {
            var backup=Path.GetFullPath(backupPath);
            var receipt=Path.GetFullPath(receiptPath);
            if(!string.Equals(receipt,backup+".sha256.txt",
                    StringComparison.OrdinalIgnoreCase))
                return false;
            var info=new FileInfo(receipt);
            if(!info.Exists || info.Length<65 || info.Length>512 ||
               (info.Attributes & FileAttributes.ReparsePoint)!=0)
                return false;
            string expected=Hash(backup)+"  "+Path.GetFileName(backup);
            string actual=File.ReadAllText(receipt,Encoding.UTF8).TrimEnd('\r','\n');
            return string.Equals(actual,expected,StringComparison.Ordinal);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or InvalidDataException)
        {
            return false;
        }
    }
}
