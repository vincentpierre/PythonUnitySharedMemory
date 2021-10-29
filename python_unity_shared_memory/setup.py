from setuptools import setup, find_packages

setup(
	name="python_unity_shared_memory",
	version="0.1.0",
	description="General purpose shared memory communication between Python and Unity",
	url="https://github.com/vincentpierre/PythonUnitySharedMemory",
	author="Vincent-Pierre Berges",
	install_requires=[],
	python_requires=">=3.6",
	packages=find_packages(
        exclude=["test"]
    ),
)
