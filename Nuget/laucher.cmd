set ver=1.0.2

xcopy lib\*.*  ..\packages\WebViewJavascriptBridgeRT.%ver%\lib  /s /i /y
BuildPublishPackage.cmd WebViewJavascriptBridgeRT %ver%


