# RueI
RueI is a hint framework, designed to be the definitive way to display multiple hints at once. you can get the latest release [here](https://github.com/pawslee/RueI/releases/latest).

if you want to develop using RueI, make sure you install the [nuget package](https://www.nuget.org/packages/RueI)

RueI also has a [website with documentation](https://pawslee.github.io/RueI/).

### Installation
RueI is a LabAPI plugin, although it works with EXILED.
1. simply download the [latest RueI 14.2 Build](https://github.com/rockysx27/RueI/releases/tag/14.2-EXILED), put the dll in the `%appdata%\SCP Secret Laboratory\LabAPI\plugins\global` folder.
2. RueI 14.2 also requires Harmony 2.4.1. You have to Options:
    1. If you run EXILED 9.10.0 (like me) then you want to cut&paste the harmony dependancy file from `%appdata%\EXILED\plugins\dependencies\0Harmony.dll` to `%appdata%\SCP Secret Laboratory\LabAPI\dependencies\global` to avoid loading more than 1 harmony dll.
    2. If you don't run EXILED then you need to download harmony dependancy [Harmony 2.4.1 Release](https://github.com/pardeike/Harmony/releases/tag/v2.4.1.0) and put the dll in the `%appdata%\SCP Secret Laboratory\LabAPI\dependencies\global` directory.


### Example
```cs
RueDisplay display = RueDisplay.Get(player);
Tag welcomeTag = new();
display.Show(welcomeTag, new BasicElement(800, "Welcome to the server!"));
display.Show(new BasicElement(300, "Don't forget to read the rules!"), 10f);
Timing.CallDelayed(5f, () =>
{
    display.Show(welcomeTag, new BasicElement(800, "New update: We added support for multiple hints at once!"), 10f);
});
```

### Features
- support for displaying multiple hints at once without them interfering with eachother
- allows using hint parameters 
- extensions for StringBuilder that makes adding tags easier 
- aspect ratio support
- extremely optimized and efficient

RueI is not a grid-based or line-based system, it calculates the offset necessary to put a hint at the same position no matter what
## Comments
if you've encountered any bugs please [make an issue](https://github.com/pawslee/RueI/issues) (it helps me out a ton)
