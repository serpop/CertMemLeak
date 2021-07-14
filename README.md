# Excessive memory usage while working with X509Certificate class

**1.** Open command prompt in the project directory.

**2.** Execute the following command to build test container:

```
powershell .\build.ps1
```

**3.** Run test container. Don't forget to enable directory sharing.

```
if not exist work mkdir work
docker run --rm --name certmemleak -v %CD%\work:/work certmemleak:latest
```

**4.** Run data collector:

```
docker exec -ti -w /root certmemleak bash -c "./dotnet-counters collect -p `./dotnet-counters ps | awk 'NR==1 {print $1}'` --refresh-interval 15 --format csv -o /work/counters"
```

**5.** Wait for test to complete (~2 hours).

**6.** Extract timestamps, GC heap size and working set size using the following commands:

```
powershell "cat .\work\counters.csv | ? { $_ -like '*GC Heap Size*' } | ? { $_ -match '\s(\d\d:\d\d:\d\d),' } | % { $Matches.1 }" | clip
powershell "cat .\work\counters.csv | ? { $_ -like '*GC Heap Size*' } | ? { $_ -match ',(\d+)$' } | % { $Matches.1 }" | clip
powershell "cat .\work\counters.csv | ? { $_ -like '*Working Set*' } | ? { $_ -match ',(\d+)$' } | % { $Matches.1 }" | clip
```

As you can see working set grows up to 1.8 GB and then stabilizes at this point. At the same time the managed heap size remains very small.
