using NUnit.Framework;
using UnityEngine;
using System.IO;
using PythonUnitySharedMemory;

namespace PythonUnitySharedMemory.Tests.Editor{
   
    public class TestSharedMemoryIntegration
    {

        [Test]
        public void TestIntegration()
        {
            SharedMemory.DeleteFiles("test");
            Debug.Log("Launch the pytest command now, the test has started.");
            // <Block u0>
            var sm = new SharedMemory("test", 100, true, 30);
            sm.WriteString(42, "foo");
            // </block u0>
            sm.GiveControl(); //block p1
            //<block u2>
            Assert.AreEqual("bar", sm.ReadString(42).Item1);
            sm.WriteString(42, "foo");
            //</block u2>
            sm.GiveControl(); // block p3

            // <block u4>
            Assert.AreEqual("bar142", sm.ReadString(142).Item1);
            Assert.AreEqual("foo", sm.ReadString(42).Item1);


            sm.Resize(300);
            // </block u4>
            sm.GiveControl(); // block p5>

            //<block u6>
            Assert.AreEqual("bar342", sm.ReadString(342).Item1);
            sm.Dispose();
            // </block u6>
        }
    }
}