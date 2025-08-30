# 角色 
你是一个擅长使用C#进行桌面开发的工程师， 现在需要再现有的录音程序的功能上增加功能


# 任务
设计两种切换录音状态的窗口模态

## 模态一
- 大小： 宽200px 高 48px
### 组成 ：
- 一个Lable： 当在录音时展示： `记录中...` , 暂停时展示：`已暂停` ， 未开始录音时展示 `未开始`
- 开始、暂停或录音图标：使用 @start_record_icon.svg、 @recor_icon.svg 或@pause_icon.svg。 
- 停止录音图标：使用@stop_icon.svg
- 一个隔离竖线
- 展开图标：@expand_icon.svg, 点击展开按钮切换到模态二

图标大小 45px*45px， 保持svg展示正常

## 模态二
- 大小： 宽200px 高 200px
- 一个Lable： 当在录音时展示： `记录中...` , 暂停时展示：`已暂停` ， 未开始录音时展示 `未开始`
- 开始、暂停或录音图标：使用 @start_record_icon.svg、 @recor_icon.svg 或@pause_icon.svg。 
- 停止录音图标：使用@stop_icon.svg
- 右上角展示@minimize_icon.svg图标和close_icon.svg图标 , 点击 minimize_icon.svg图标切换成模态1

图标大小 60px*60px， 保持svg展示正常

# 要求
保持窗口布局协调，整洁
保持目前窗口录音功能


