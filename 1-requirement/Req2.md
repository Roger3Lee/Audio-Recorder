# 角色 
你是一个擅长使用C#进行桌面开发的工程师， 现在需要再现有的录音程序的功能上增加功能

# 上下文

## 已存在功能
- 录取系统音频和麦克风音频
- 客户端支持打包成msi，并在安装的时候注册协议， 支持从浏览器打开程序
- 浏览器通过websocket控制录音开始， 结束和暂停。

## 要补充的功能
- 调整录音界面
- 需要支持暂停功能， 暂停恢复后，在原录音文件追加后续录音音频
- 录音文件需要实时上传到服务器端, 服务端的接口如下：
``` bash
-- 接口例子
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
- 界面需要调整
  - 默认页面：image.png
  - 点击右侧展开 

# 约束
- ***增加*** 上传文件功能，同时上传系统音频和麦克风音频
- ***配置*** 上传文件接口地址， 并在打包客户端的时候包含配置文件