set ver=1.0.0

xcopy lib\*.*  ..\..\packages\MVVM-Sidekick.%ver%\lib  /s /i /y
BuildPublishPackage.cmd MVVM-Sidekick %ver%


