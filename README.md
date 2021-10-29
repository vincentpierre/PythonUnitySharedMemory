# PythonUnitySharedMemory
Using a shared file to exchange data between Unity and Python.

## Functionality
 - Can only exchange booleans, integers, floats, strings and byte arrays. 
 - Easy to add new types.
 - Lightweight : Only one [script on C#](com.python-unity-shared-memory/Runtime/Core.cs) and one [script on Python](python_unity_shared_memory/python_unity_shared_memory/__init__.py)
 - Shared memory not work across machines like sockets would.
 - Shared memory has the potential to be extremely fast when the volume of data is large.

## Recommended Installation
### Unity
Via the Package Manager. Add a dependency to the `manifest.json` of your project:
```
{
  "dependencies": {
    "com.python-unity-shared-memory": "https://github.com/vincentpierre/PythonUnitySharedMemory.git?path=/com.python-unity-shared-memory"
  }
}
```
Alternatively, you can clone the repository locally and use you local path.

### Python
Clone the repository and run the command 
```
pip install -e python_unity_shared_memory
```

## API
The API is meant to be identical between C# and Python.

### Create a shared memory file

#### Python

```
from python_unity_shared_memory import  SharedMemory, delete_files

sm = SharedMemory(
    prefix="my-file",   # The ID of the shared memory 
    capacity=100,       # The number of bytes of the file
    create_new=False,   # Wether to create the file or open it
    timeout=30)         # How low to timeout (-1 means no timeout)
```

#### C#

```
using PythonUnitySharedMemory;

var sm = new SharedMemory(
    prefix:"my-file",    // The ID of the shared memory 
    capacity:100,       // The number of bytes of the file
    createNew:true,     // Wether to create the file or open it
    timeout:30);        // How low to timeout (-1 means no timeout)
```

#### Notes

Note that :
 - In the example above, the id "my-file" need to be identical for the two shared memory partitions to communicate. 
 - You can open as many shared memory files as you want.
 - The `capacity` must be the same on both sizes.
 - In this case, Unity creates the share memory and Python joins it (see `create_new`/`createNew` arguments)
 - `timeout` is expressed in seconds
 - A negative `timeout` means no timeout

### Resizing

#### Python

```
sm.resize(new_capacity)
```

#### C#

```
sm.Resize(newCapacity)
```

#### Notes
If your shared memory partition is too small, you can resize it. If you do, the new capacity must be strictly greater than the previous one. When resizing, a new file will be created and both SharedMemory objects will switch to using the new one. This operation is expensive, it is recommended to use it only when necessary and to allocate a greater capacity than needed.

### Read and write
#### Python

Available read methods take as argument an offset in the shared memory and return a tuple of (value, new_offset):
 - `read_bool`
 - `read_int32`
 - `read_float32`
 - `read_string`
 - `read_bytes` (takes as input an offset and a length argument)

And the write methods take as argument an offset and a value. They will return the new offset.
 - `write_bool`
 - `write_int32`
 - `write_float32`
 - `write_string`
 - `write_bytes`

For example:
```
new_offset = sm.write_string(offset, "my-string")
my_string, new_offset = sm.read_string(offset)
assert my_string == "my-string"
```

#### C#

Available read methods take as argument an offset in the shared memory and return a tuple of (value, new_offset):
 - `ReadBool`
 - `ReadInt32`
 - `ReadFloat32`
 - `ReadString`
 - `ReadBytes` (takes as input an offset and a *length* argument)

And the write methods take as argument an offset and a value. They will return the new offset.
 - `WriteBool`
 - `WriteInt32`
 - `WriteFloat32`
 - `WriteString`
 - `WriteBytes`

For example:
```
newOffset = sm.WriteString(offset, "my-string")
(str my_string, int new_offset) = sm.ReadString(offset)
assert my_string == "my-string"
```
#### Notes
The reason the read and write methods return the new offset is to make it easy to chain reads and writes:
```
offset = 0
value_1, offset = sm.read_int32(offset)     # offset is now 4
value_2, offset = sm.read_float32(offset)   # offset is now 8
```

Note also that C# tuples are accessed with the properties `tuple.Item1` and `tuple.Item2` while Python can access tuples like arrays `tuple[0]` and `tuple[1]`

### Synchronicity
The `give_control()` and `GiveControl()` methods will signal the other process that it is their turn to edit the file. Until the other process gives control back, the current process will be waiting. 
It is possible to pass an optional argument `wait=False` to `give_control` on both Python and C#. In this case the control will be passed to the other process but the current process will not be blocked. You can manually block the process until control is given back with `wait_unblocked`/`WaitUnblocked`. Note that if you give back control without waiting *and* edit the shared memory before calling `wait_unblocked` you expose yourself to *race conditions*. 

### Terminating
You can call the `close()`/`Close()` methods to signal the file is no longer being used and call `delete()/Delete()` to delete it. If you want to manually delete your old shared memory files, you can call the method `delete_files(prefix)` on Python and `SharedMemory.DeleteFiles(prefix)` on C#.