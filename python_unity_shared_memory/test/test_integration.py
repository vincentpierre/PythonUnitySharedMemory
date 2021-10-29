from python_unity_shared_memory import  SharedMemory, delete_files
import os
import pytest

def test_integration():
    sm = SharedMemory("test", 100, False, 30)
    # <block p1>
    assert sm.read_string(42)[0] == "foo"
    sm.write_string(42, "bar")
    assert sm.read_string(42)[0] == "bar"
    # </block p1>
    sm.give_control() # bock u2
    # <block p3>
    assert sm.read_string(42)[0] == "foo"

    sm.resize(200)
    sm.write_string(142, "bar142")
    #</block p3>
    sm.give_control() # block u4
    #<block p5>
    sm.resize(400)
    sm.write_string(342, "bar342")
    #</block p5>
    sm.give_control(wait=False) # block u6

