# YAZip
### Yet Another Zip
Made by Nordgaren  

This program will bundle up any folder into a bhd/bdt pair. It can also unpack the bhd/bdt pair.  
supports individual files up to 2GB-ish (max value of an int)  

This tool uses SOME modified code from [UXM](https://github.com/JKAnderson/UXM) and [Yabber](https://github.com/JKAnderson/Yabber)  
Using [SoulsFormats](https://github.com/JKAnderson/SoulsFormats) by JK Anderson  
Dependencies packaged into EXE by [Costrua.Fody](https://www.nuget.org/packages/Costura.Fody/) NuGet 

### Instructions
1) Drag folder or bhd or bdt onto exe. Folders will be packed, bhds or bdts will look for it's pair and unpack.
2) When packing folded, answer yes to add a password and encrypt bhd file.

#### Optional:  
Both options are case sensitive  
* Name file or folder with .Encrypt - Encrypts each file in bdt that contains .Encrypt all the way through.
* Name file or fodler with . DCX - compresses file marked with .DCX. Capitalization **WARNING** using .DCX on large files requires a lot of memory right now. Do not compress large files if you don't want to run out of memory!

### Thank You
 
**[TKGP](https://github.com/JKAnderson)** for making SoulsFormats, UXM, and Yabber.  

### Patch Notes  
## V 1.1
* Patched bug thanks to **[NamelessHoodie](https://github.com/NamelessHoodie/)** and other small meme fixes
## V 1
* Release!


