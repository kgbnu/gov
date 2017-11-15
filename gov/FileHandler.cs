using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace gov
{
    class FileHandler
    {
        public static void DeleteFilesIfExistsDir(string path)
        {
            if(!Directory.Exists(path))
                return;
            DirectoryInfo dir = new DirectoryInfo(path);
            FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  //返回目录中所有文件和子目录
            foreach (FileSystemInfo i in fileinfo)
            {
                if (i is DirectoryInfo)            //判断是否文件夹
                {
                    DirectoryInfo subdir = new DirectoryInfo(i.FullName);
                    subdir.Delete(true);          //删除子目录和文件
                }
                else
                {
                    File.Delete(i.FullName);      //删除指定文件
                }
            }
        }
    }
}
