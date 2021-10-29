using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace PythonUnitySharedMemory
{
    unsafe internal class Core 
    {
        internal const string k_Dir= "python_unity_shared_memory";
        public MemoryMappedViewAccessor Mem;
        public string FilePath;

        public Core(string name, int capacity, bool createNew)
        {
            if (name.Length < 1){
                throw new ArgumentException("Name must be more than one character long");
            }
            var directory = Path.Combine(Path.GetTempPath(), k_Dir);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            FilePath = Path.Combine(directory, name);
            if (createNew)
            {
                if (File.Exists(FilePath))
                {
                    throw new IOException($"The file {name} already exists");
                }
                using (var fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write))
                {
                    // Clear existing data
                    fs.Write(new byte[capacity], 0, capacity);
                }
            }
            long length = new System.IO.FileInfo(FilePath).Length;
            var mmf = MemoryMappedFile.CreateFromFile(
                File.Open(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite),
                null,
                length,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                false
            );
            Mem = mmf.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);
            mmf.Dispose();
        }

        public void Close()
        {
            if (Opened)
            {
                Mem.SafeMemoryMappedViewHandle.ReleasePointer();
                Mem.Dispose();
            }
        }

        protected bool Opened
        {
            get
            {
                return Mem.CanWrite;
            }
        }

        public void Delete()
        {
            Close();
            File.Delete(FilePath);
        }
    

        public (int,int) ReadInt32(int offset)
        {
            var val =  Mem.ReadInt32(offset);
            return (val, offset + 4);
        }
        public (bool, int) ReadBool(int offset)
        {
            var val = Mem.ReadBoolean(offset);
            return (val, offset + 1);
        }
        public (byte[], int) ReadBytes(int offset, int length){
            var arr = new byte[length];
            Mem.ReadArray(offset, arr, 0, length);
            return (arr, offset+length);
        }
        public int WriteInt32(int offset, int value)
        {
            Mem.Write(offset, value);
            return offset + 4;
        }
        public int WriteBool(int offset, bool value)
        {
            Mem.Write(offset, value);
            return offset + 1;
        }
        public int WriteBytes(int offset, byte[] data){
            Mem.WriteArray(offset, data, 0, data.Length);
            return offset + data.Length;
        }

    }


    public class SharedMemory{
        private const int k_Version = 1;
        private const int k_VersionOffset = 0;
        private const int k_FileNumberOffset=4;
        private const int k_CapacityOffset=8;
        private const int k_PythonActiveOffset=12;
        private const int  k_UnityActiveOffset=13;
        private const int k_CloseOffset=14;
        private int m_LastFileNumber;

        private string m_Name;
        private float m_Timeout;
        private Core m_Hook;
        private Core m_Current;

        public static void  DeleteFiles(string prefix){
            var directory = Path.Combine(Path.GetTempPath(), Core.k_Dir);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            DirectoryInfo dir = new DirectoryInfo(directory);
            FileInfo[] files = dir.GetFiles(prefix + "*");
            foreach(FileInfo f in files){
                f.Delete();
            }
        }

        public SharedMemory(string prefix, int capacity, bool createNew, float timeout){
            m_Name = prefix;
            m_Timeout = timeout;
            if (m_Name[m_Name.Length - 1] == '_'){
                throw new ArgumentException("The name cannot end with a _.");
            }
            m_Hook = new Core(m_Name, k_CloseOffset + 1, createNew);
            if(createNew){
                Version = k_Version;
                FileNumber = 1;
                Capacity = capacity;
                m_Hook.WriteBool(k_UnityActiveOffset, true);
            }
            else{
                if(Version != k_Version){
                    throw new Exception($"Incompatible Versions between Unity (v{k_Version}) and Python (v{Version})");
                }
            }
            m_LastFileNumber = FileNumber;
            m_Current = new Core(m_Name + new string('_', FileNumber), Capacity, createNew);
            WaitUnblocked();
        }

        public void Resize(int capacity){
            FileNumber += 1;
            int oldCapacity = Capacity;
            if (capacity < oldCapacity){
                throw new ArgumentException("New capacity must be larget than old capacity.");
            }
            Capacity = capacity;
            var newCurrent = new Core(m_Name + new string('_', FileNumber), Capacity, true);
            newCurrent.WriteBytes(0, m_Current.ReadBytes(0, oldCapacity).Item1);
            m_Current = newCurrent;
            m_LastFileNumber = FileNumber;
        }

        public void WaitUnblocked(){
            if (Blocked){
                var t0 = DateTime.Now.Ticks;
                int iteration = 0;
                while(Blocked && Active){
                    if (iteration % 1e8 == 0 && m_Timeout > 0){
                        if (DateTime.Now.Ticks - t0 > m_Timeout * 1e7){
                            Delete();
                            throw new TimeoutException("Communication took too long.");
                        }
                    }
                }
            }
            if (!Active){
                Delete();
                throw new Exception("Python has stopped.");
            }
            if(m_LastFileNumber != FileNumber){
                m_LastFileNumber = FileNumber;
                m_Current.Delete();
                m_Current = new Core(m_Name+ new string('_', FileNumber), Capacity, false);
            }
        }

        public void Close(){
            m_Hook.WriteBool(k_CloseOffset,true);
            m_Hook.Close();
            m_Current.Delete();
        }
        public void Delete(){
            Close();
            m_Hook.Delete();
            m_Current.Delete();
        }

        public void Dispose(){
            Delete();
        }



        private int Version{
            get{return m_Hook.ReadInt32(k_VersionOffset).Item1;}
            set{m_Hook.WriteInt32(k_VersionOffset, value);}
        }
        private int FileNumber{
            get{return m_Hook.ReadInt32(k_FileNumberOffset).Item1;}
            set{m_Hook.WriteInt32(k_FileNumberOffset, value);}
        }
        private int Capacity{
            get{return m_Hook.ReadInt32(k_CapacityOffset).Item1;}
            set{m_Hook.WriteInt32(k_CapacityOffset, value);}
        }
        private bool Active{
            get{return !m_Hook.ReadBool(k_CloseOffset).Item1;}
        }

        public void UnsafeSignalPythonUnblocked(){
            m_Hook.WriteBool(k_PythonActiveOffset, true);
        }
        public void UnsafeSignalUnityBlocked(){
            m_Hook.WriteBool(k_UnityActiveOffset, false);
        }
        public void GiveControl(bool wait = true){
            WaitUnblocked();
            UnsafeSignalUnityBlocked();
            UnsafeSignalPythonUnblocked();
            if (wait){
                WaitUnblocked();
            }
        }
        public bool Blocked{
            get{return !m_Hook.ReadBool(k_UnityActiveOffset).Item1;}
        }

        // The read methods
        public (int, int) ReadInt32(int offset){
            WaitUnblocked();
            return m_Current.ReadInt32(offset);
        }
        public (bool, int) ReadBool(int offset){
            WaitUnblocked();
            return m_Current.ReadBool(offset);
        }
        public (byte[], int) ReadBytes(int offset, int length){
            WaitUnblocked();
            return m_Current.ReadBytes(offset, length);
        }
        public (float, int) ReadFloat32(int offset){
            WaitUnblocked();
            return (m_Current.Mem.ReadSingle(offset), offset + 4);
        }
        public (string, int) ReadString(int offset){
            WaitUnblocked();
            (int length, _) = ReadInt32(offset);
            (var data, _) = ReadBytes(offset + 4, length);
            return (Encoding.ASCII.GetString(data), offset + 4 + length);
            
        }


        // The write methods
        public int WriteInt32(int offset, int value){
            WaitUnblocked();
            return m_Current.WriteInt32(offset, value);
        }
        public int WriteBool(int offset, bool value){
            WaitUnblocked();
            return m_Current.WriteBool(offset, value);
        }
        public int WriteBytes(int offset, byte[] data){
            WaitUnblocked();
            return m_Current.WriteBytes(offset, data);
        }
        public int WriteFloat32(int offset, float value){
            WaitUnblocked();
            m_Current.Mem.Write(offset, value);
            return offset + 4;
        }

        public int WriteString(int offset, string value){
            WaitUnblocked();
            var length = value.Length;
            offset = WriteInt32(offset, length);
            return WriteBytes(offset, Encoding.ASCII.GetBytes(value));
        }

    }
}