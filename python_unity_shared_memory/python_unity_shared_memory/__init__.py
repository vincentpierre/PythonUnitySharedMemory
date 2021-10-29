import os
import tempfile
import mmap
import struct
import time
from typing import Tuple
import glob

class _Core:
    DIR = "python_unity_shared_memory"

    def __init__(self, name:str, capacity:int, create_new:bool):
        """
        Creates (if `create_new` is True) or opens (if `create_new`
        is False) the file `name` with size `capacity`
        in bytes (if opening a file `capacity cannot be bigger than 
        the file)
        """
        self.opened = False
        if len(name) < 1:
            raise ValueError("name must be at least one character")
        directory = os.path.join(tempfile.gettempdir(), self.DIR)
        if not os.path.exists(directory):
            os.makedirs(directory)
        # At this point, the directory DIR has been created
        self.file_path = os.path.join(directory, name)
        if create_new:
            if os.path.exists(self.file_path):
                raise FileExistsError(f"The file {name} already exists.")
            # Clear any data present in the file within capacity
            empty_array = bytearray(capacity)
            with open(self.file_path, "w+b") as f:
                f.write(empty_array)
        with open(self.file_path, "r+b") as f:
            self.mem = mmap.mmap(f.fileno(), 0) #0 means whole file
        if len(self.mem) < capacity:
            raise ValueError(
                f"The file {name} is smaller than the requested capacity"
            )
        self.opened = True
    
    def close(self) -> None:
        """
        Closes the file and removes access. Does not delete.
        """
        if self.opened:
            self.mem.close()
            self.opened = False
    
    def delete(self):
        self.close()
        try:
            os.remove(self.file_path)
        except BaseException:
            pass
    
    def __enter__(self):
        return self
    
    def __exit__(self, *a):
        self.delete()


    def read_int32(self, offset) -> Tuple[int, int]:
        return struct.unpack_from("<i", self.mem, offset)[0], offset + 4
    def read_bool(self, offset) -> Tuple[bool, int]:
        return struct.unpack_from("<?", self.mem, offset)[0], offset + 1
    def read_bytes(self, offset, length) ->Tuple[bytes, int]:
        return self.mem[offset:offset+length], offset+length

    
    # Now the write methods
    # write methods have argument offset and value and return a new offset
    def write_int32(self, offset:int, value:int) -> int:
        struct.pack_into("<i", self.mem, offset, value)
        return offset + 4
    def write_bool(self, offset:int, value:bool) -> int:
        struct.pack_into("<?", self.mem, offset, value)
        return offset + 1
    def write_bytes(self, offset:int, data:bytes) -> int:
        length = len(data)
        self.mem[offset:offset+length] = data
        return offset+length

def delete_files(prefix:str):
    directory = os.path.join(tempfile.gettempdir(), _Core.DIR)
    if not os.path.exists(directory):
        os.makedirs(directory)
    file_path = os.path.join(directory, prefix)
    for f in glob.glob(file_path + "*"):
        os.remove(f)

class SharedMemory:

    _VERSION = 1
    _VERSION_OFFSET = 0
    _FILE_NUMBER_OFFSET = 4
    _CAPACITY_OFFSET = 8
    _PYTHON_ACTIVE_OFFSET = 12
    _UNITY_ACTIVE_OFFSET = 13
    _CLOSE_OFFSET = 14
    def __init__(
        self, 
        prefix:str, 
        capacity:int, 
        create_new:bool,
        timeout: int = -1
        ):
        """
        DATA          |  TYPE   | OFFSET
        ______________|_________|_______
        VERSION       |  int    | 0
        FILE_NUMBER   |  int    | 4
        CAPACITY      |  int    | 8
        PYTHON_ACTIVE |  bool   | 12
        UNITY_ACTIVE  |  bool   | 13
        CLOSE         |  bool   | 14
        ________________________________

        """
        self._name = prefix
        self._timeout = timeout
        if self._name[-1] == '_':
            raise ValueError(
            f"The name of the shared memory {name} cannot end with _."
            )
        self._hook = _Core(self._name, self._CLOSE_OFFSET+1, create_new)
        if create_new:
            #must write the hook's data
            self._version = self._VERSION
            self._file_number = 1
            self._capacity = capacity
            self._hook.write_bool(self._PYTHON_ACTIVE_OFFSET, True) # Python is active
        else:
            if self._version != self._VERSION:
                raise RuntimeError(
                f"Incompatible versions between Unity (v{self._version}) and Python (v{self._VERSION})"
                )
        self._last_file_number = self._file_number
        self._current = _Core(self._name + '_' * self._file_number, self._capacity, create_new)
        self.wait_unblocked()

    def resize(self, capacity:int):
        self._file_number += 1
        old_capacity = self._capacity
        if capacity < old_capacity:
            raise ValueError("New capacity must be greater than previous")
        self._capacity = capacity
        new_current = _Core(self._name + '_' * self._file_number, self._capacity, True)
        new_current.mem[0:old_capacity] = self._current.mem[0:capacity]
        self._current = new_current
        self._last_file_number = self._file_number

    def wait_unblocked(self):
        if self.blocked:
            t0 = time.time()
            iteration = 0
            while self.blocked and self._active:
                if iteration % 1e8 == 0 and self._timeout > 0:
                    if time.time()- t0> self._timeout:
                        self.delete()
                        raise TimeoutError("Communication took too long.")
        if not self._active:
            self.delete()
            raise ConnectionError("Unity has stopped.")
        if self._last_file_number != self._file_number:
            self._last_file_number = self._file_number
            self._current.delete()
            self._current = _Core(self._name + '_' * self._file_number, self._capacity, False)

    def close(self) -> None:
        try:
            self._hook.write_bool(self._CLOSE_OFFSET, True)
        except BaseException:
            pass
        self._hook.close()
        self._current.close()
    
    def delete(self):
        self.close()
        self._hook.delete()
        self._current.delete()

    def __enter__(self):
        return self
    
    def __exit__(self, *a):
        self.delete()


    @property
    def _version(self):
        return self._hook.read_int32(self._VERSION_OFFSET)[0]
    @_version.setter
    def _version(self, value):
        self._hook.write_int32(self._VERSION_OFFSET, value)
    @property
    def _file_number(self):
        return self._hook.read_int32(self._FILE_NUMBER_OFFSET)[0]
    @_file_number.setter
    def _file_number(self, value):
        self._hook.write_int32(self._FILE_NUMBER_OFFSET, value)
    @property
    def _capacity(self):
        return self._hook.read_int32(self._CAPACITY_OFFSET)[0]
    @_capacity.setter
    def _capacity(self, value):
        self._hook.write_int32(self._CAPACITY_OFFSET, value)
    @property
    def _active(self):
        return not self._hook.read_bool(self._CLOSE_OFFSET)[0]
    
    def unsafe_signal_unity_unblocked(self):
        self._hook.write_bool(self._UNITY_ACTIVE_OFFSET, True)
    def unsafe_signal_python_blocked(self):
        self._hook.write_bool(self._PYTHON_ACTIVE_OFFSET, False)
    def give_control(self, wait:bool = True):
        self.wait_unblocked()
        self.unsafe_signal_python_blocked()
        self.unsafe_signal_unity_unblocked()
        if wait:
            self.wait_unblocked()
    @property
    def blocked(self):
        return not self._hook.read_bool(self._PYTHON_ACTIVE_OFFSET)[0]


    # This region is all the read files
    # You can add more here
    # All read methods return a value and the new offset
    def read_int32(self, offset) -> Tuple[int, int]:
        self.wait_unblocked()
        return self._current.read_int32(offset)
    def read_bool(self, offset) -> Tuple[bool, int]:
        self.wait_unblocked()
        return self._current.read_bool(offset)
    def read_bytes(self, offset, length)-> Tuple[bytes, int]:
        self.wait_unblocked()
        return self._current.read_bytes(offset, length)
    def read_float32(self, offset) -> Tuple[float, int]:
        self.wait_unblocked()
        return struct.unpack_from("<f", self._current.mem, offset)[0], offset + 4
    def read_string(self, offset) -> Tuple[str, int]:
        """
        The string is encoded with its length first is ASCII
        """
        self.wait_unblocked()
        length, offset = self.read_int32(offset)
        data, offset = self.read_bytes(offset, length)
        return data.decode("ascii"), offset


    # Now the write methods
    # write methods have argument offset and value and return a new offset
    def write_int32(self, offset:int, value:int) -> int:
        self.wait_unblocked()
        return self._current.write_int32(offset, value)
    def write_bool(self, offset:int, value:bool) -> int:
        self.wait_unblocked()
        return self._current.write_bool(offset, value)
    def write_bytes(self, offset: int, data:bytes) -> int:
        return self._current.write_bytes(offset, data)
    def write_float32(self, offset:int, value:float) -> int:
        self.wait_unblocked()
        struct.pack_into("<f", self._current.mem, offset, value)
        return offset + 4
    def write_string(self, offset, value:str) -> int:
        self.wait_unblocked()
        length = len(value)
        offset = self.write_int32(offset, length)
        self._current.mem[offset:offset+length] = value.encode("ascii")
        return offset + length