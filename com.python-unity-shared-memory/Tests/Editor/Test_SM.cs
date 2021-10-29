using NUnit.Framework;
using UnityEngine;
using System.IO;
using PythonUnitySharedMemory;

namespace PythonUnitySharedMemory.Tests.Editor{
   
    public class TestSharedMemory
    {

        [Test]
        public void TestCreate()
        {
            SharedMemory.DeleteFiles("test");
            var sm = new SharedMemory("test", 100, true, 30);
            sm.Close();

            var path = Path.Combine(Path.GetTempPath(), "python_unity_shared_memory/test");
            Assert.True(File.Exists(path));
            SharedMemory.DeleteFiles("test");
            Assert.False(File.Exists(path));
        }

        [Test]
        public void TestReadWrite(){
            SharedMemory.DeleteFiles("test");
            var sm = new SharedMemory("test", 100, true, 30);

            Assert.False(sm.ReadBool(0).Item1);
            sm.WriteBool(0, true);
            Assert.True(sm.ReadBool(0).Item1);

            Assert.AreEqual(sm.ReadInt32(3).Item1, 0);
            sm.WriteInt32(3, 42);
            Assert.AreEqual(sm.ReadInt32(3).Item1, 42);


            // Assert.AreApproximatelyEqual(sm.ReadFloat32(3).Item1, 0);
            // sm.WriteFloat32(3, 42);
            // comparer.AreApproximatelyEqual(sm.ReadFloat32(3).Item1, 42);

            sm.WriteString(10, "forty-two");
            Assert.AreEqual(sm.ReadString(10).Item1, "forty-two");

            var path = Path.Combine(Path.GetTempPath(), "python_unity_shared_memory/test");
            Assert.True(File.Exists(path));
            sm.Delete();
            Assert.False(File.Exists(path));
        }

        [Test]
        public void TestTimeout(){
            SharedMemory.DeleteFiles("test");
            var sm = new SharedMemory("test", 100, true, 0.1f);
            sm.WriteBool(0, true);
            sm.GiveControl(wait:false);
            Assert.Throws<System.TimeoutException>(() => sm.ReadBool(0));
        }

        [Test]
        public void TestTwoUnity(){
            SharedMemory.DeleteFiles("test");
            var sm0 = new SharedMemory("test", 100, true, 30);
            var sm1 = new SharedMemory("test", 100, false, 30);
            sm0.WriteString(42, "foo");
            Assert.AreEqual(sm1.ReadString(42).Item1, "foo");
            SharedMemory.DeleteFiles("test");
        }

        [Test]
        public void TestResize(){
            SharedMemory.DeleteFiles("test");
            var sm0 = new SharedMemory("test", 100, true, 30);
            var sm1 = new SharedMemory("test", 100, false, 30);
            sm0.WriteString(42, "foo");
            Assert.AreEqual(sm1.ReadString(42).Item1, "foo");
            sm1.Resize(101);
            Assert.AreEqual(sm0.ReadString(42).Item1, "foo");
            Assert.AreEqual(sm1.ReadString(42).Item1, "foo");
            sm0.Close();
            Assert.Throws<System.Exception>(() => sm1.ReadBool(0));
            SharedMemory.DeleteFiles("test");
        }
    }
}