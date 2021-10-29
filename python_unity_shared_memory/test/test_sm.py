from python_unity_shared_memory import  SharedMemory, delete_files
import os
import pytest

def test_create():
    delete_files("test")
    sm = SharedMemory("test", 100, True, 30)
    sm.close()
    assert os.path.exists(sm._hook.file_path)
    delete_files("test")
    assert not os.path.exists(sm._hook.file_path)
    
def test_read_write():
    delete_files("test")
    sm = SharedMemory("test", 100, True, 30)

    assert not sm.read_bool(0)[0]
    sm.write_bool(0, True)
    assert sm.read_bool(0)[0]

    assert sm.read_int32(3)[0] == 0
    sm.write_int32(3, 42)
    assert sm.read_int32(3)[0] == 42

    assert sm.read_float32(10)[0] == 0
    sm.write_float32(10, 42)
    assert sm.read_float32(10)[0] == 42

    sm.write_string(10, "forty-two")
    assert sm.read_string(10)[0] == "forty-two"

    assert os.path.exists(sm._hook.file_path)
    sm.delete()
    assert not os.path.exists(sm._hook.file_path)
    
def test_timeout():
    delete_files("test")
    sm = SharedMemory("test", 100, True, 0.01)
    sm.write_bool(0, True)
    sm.give_control(wait = False)
    with pytest.raises(TimeoutError):
        sm.read_bool(0)

def test_two_python():
    delete_files("test")
    sm0 = SharedMemory("test", 100, True, 30)
    sm1 = SharedMemory("test", 100, False, 30)
    sm0.write_string(42, "foo")
    assert sm1.read_string(42)[0] == "foo"
    delete_files("test")

def test_resize():
    delete_files("test")
    sm0 = SharedMemory("test", 100, True, 0)
    sm1 = SharedMemory("test", 100, False, 0)
    sm0.write_string(42, "foo")
    assert sm1.read_string(42)[0] == "foo"
    sm1.resize(101)
    assert sm0.read_string(42)[0] == "foo"
    assert sm1.read_string(42)[0] == "foo"

    sm0.close()
    with pytest.raises(ConnectionError):
        sm1.read_bool(0)
    delete_files("test")
