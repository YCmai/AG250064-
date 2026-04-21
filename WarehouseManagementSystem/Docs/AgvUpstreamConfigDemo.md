# AGV主动上报配置示例

`AgvUpstream` 是运行时实际生效配置，建议按现场环境填写。

```json
"AgvUpstream": {
  "TimeoutSeconds": 10,
  "MaxRetryCount": 10,
  "MaterialArrivedEndpoint": "http://10.200.178.88:5000/api/ApiTask/materialarrived",
  "SafetySignalEndpoint": "http://10.200.178.88:5000/api/ApiTask/safetysignal",
  "JobCompletedEndpoint": "http://10.200.178.88:5000/api/ApiTask/jobcompleted"
}
```

`AgvUpstreamDemo` 仅为示例，不参与运行逻辑。
