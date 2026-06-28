Tool modded to work without the 3rd party addition used by the original auther.

PeaceWalkerTools - drag a file onto the exe, or pass paths as arguments.
Support PS3 & PSP version, but for PS3 SLOT.DAT you have to use *(https://github.com/abuali129/Chrysalis-1.3)*

**.NET 7.0 Runtime** **(https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-7.0.20-windows-x64-installer?cid=getdotnetcore)** is needed to run the tool
```
Supported files:
  *.dar              Unpack DAR archive  → <name>_dar\  +  <name>.dar.inf
  *.qar              Unpack QAR archive  → <name>_qar\  +  <name>.qar.inf
  *.txp              Extract TXP textures → PNG files next to the .txp
  *.dar.inf          Repack DAR from manifest
  *.qar.inf          Repack QAR from manifest
  STAGEDAT.PDT       Decrypt & extract all stage files → STAGEDAT_pdt\
  SLOT.DAT           Decrypt & extract all slot files  → SLOT\  (needs SLOT.KEY in same folder)
  *.slot             Unpack a single .slot file → individual sub-files + .slot.xml manifest
  *.slot.xml         Repack .slot from manifest, then patch back into SLOT.DAT
```
=========================================================
#Original README.md
# PeaceWalkerTools
Pack/Unpack Tools for Metal Gear Solid : Peace Walker PSP

PSP판 피스워커를 한국어화 하기위해서 만들었습니다.
개인적인 용도로 만든거라 코트가 정리되지 않아 엉망입니다.
차차 리팩토링 해  나갈 예정입니다.
