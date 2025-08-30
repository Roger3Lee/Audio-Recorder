# 角色 
你是一个擅长使用C#进行桌面开发的工程师， 现在需要再现有的录音程序的功能上增加功能

# 上下文

## 已存在功能
- 录取系统音频和麦克风音频
- 客户端支持打包成msi，并在安装的时候注册协议， 支持从浏览器打开程序
- 浏览器通过websocket控制录音开始， 结束和暂停。

## 要补充的功能
- 录音文件需要实时上传到服务器端
```
客户端用到文件上传接口
/admin-api/asr/file/upload-multiple
接口例子
curl --request POST \
  --url http://10.10.21.67:38080/admin-api/asr/file/upload-multiple \
  --header 'Accept: */*' \
  --header 'Accept-Encoding: gzip, deflate, br' \
  --header 'Authorization: 01809869aa1b4c98903495da6e00e11c' \
  --header 'Cache-Control: no-cache' \
  --header 'Connection: keep-alive' \
  --header 'Content-Length: 1331917' \
  --header 'Host: 10.10.21.67:38080' \
  --header 'User-Agent: PostmanRuntime-ApipostRuntime/1.1.0' \
  --header 'content-type: multipart/form-data' \
  --form 'files=@[object Object]' \
  --form bizType=asr \
  --form 'files=@[object Object]' \
  --form mergeAudio=true
```