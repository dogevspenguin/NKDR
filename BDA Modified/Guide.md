# Self Compile
* Install Unity 2019.4.18f1
* Download the entire github
* Open the .sln in visual studio (Require the latest visual studio and .NET 8.0)
* References will be missing, Screenshot them
* Select everything in Reference except The ones with "System" in them
* From your screenshot, Find all the .dll from:
    * KSP/KSP_x64_Data/Managed
    * Unity/Editor/Data/Managed/Unity Engine
* Add them all
* Now, you can edit the code to your liking
* Build the project
* Replace BDA+'s .dll with the resulting .dll
