# API Endpoints

Base URL (local): `http://localhost:7071/api`

> **Auth (dev):** Thêm header `X-User-Id: <guid>` vào các request yêu cầu xác thực.

---

## Health

| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/health` | Không |

Kiểm tra trạng thái hoạt động của service. Trả về `{ Status, Timestamp, Service }`.

---

## Workflows

### Tạo workflow mới
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/workflows` | Có |

Tạo một workflow mới ở trạng thái **Draft**. Request body chứa tên, mô tả, danh sách steps (mỗi step có `name`, `stepType`, `configuration`, `requiredRole`, `timeoutMinutes`). Trả về `WorkflowId` với HTTP 201.

**Request body:**
```json
{
  "name": "string",
  "description": "string?",
  "templateId": "guid?",
  "steps": [
    {
      "name": "string",
      "stepType": "Notification | HumanApproval | Action | Condition",
      "configuration": "string?",
      "requiredRole": "string?",
      "timeoutMinutes": 60,
      "onTimeoutAction": "string?"
    }
  ]
}
```

---

### Lấy danh sách workflow
| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/workflows` | Không |

Trả về toàn bộ danh sách workflow đang lưu trữ trong Blob Storage.

---

### Lấy chi tiết workflow
| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/workflows/{workflowId}` | Không |

Lấy thông tin chi tiết của một workflow theo ID, bao gồm các steps, trạng thái và `LogicAppResourceId` (nếu đã deploy).

---

### Cập nhật workflow
| Method | Route | Auth |
|--------|-------|------|
| `PUT` | `/workflows/{workflowId}` | Không |

Cập nhật tên và mô tả của workflow. Chỉ áp dụng cho workflow ở trạng thái **Draft**.

**Request body:**
```json
{
  "name": "string",
  "description": "string?"
}
```

---

### Xóa workflow
| Method | Route | Auth |
|--------|-------|------|
| `DELETE` | `/workflows/{workflowId}` | Không |

Xóa workflow khỏi Blob Storage.

---

### Kích hoạt workflow (Activate + Deploy to Logic App)
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/workflows/{workflowId}/activate` | Không |

Chuyển workflow từ trạng thái **Draft** sang **Active**, đồng thời:
1. Sinh ARM template JSON cho Azure Logic App từ các steps của workflow.
2. Deploy ARM template lên Azure (subscription `AzureResources__SubscriptionId`, resource group `AzureResources__ResourceGroupName`) nếu `DeployEnabled = true`.
3. Lưu `LogicAppResourceId` trở lại vào workflow.

Trả về `{ ArmTemplateContent, FileName, LogicAppResourceId }`.

---

### Thực thi workflow
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/workflows/{workflowId}/execute` | Không |

Khởi chạy một phiên thực thi (Execution) mới cho workflow đã được **Active**. Gửi sự kiện `WorkflowStarted` vào Service Bus. Trả về HTTP 202 Accepted.

**Request body:**
```json
{
  "inputData": "string?"
}
```

---

## Connectors

### Tạo connector mới
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/connectors` | Không |

Tạo một connector tích hợp (Slack, Teams, HTTP, v.v.). Nếu `authModel` là OAuth, hệ thống tạo URL đồng ý OAuth và trả về trong `OAuthConsentUrl`. Trả về `{ ConnectorId, OAuthConsentUrl? }` với HTTP 201.

**Request body:**
```json
{
  "name": "string",
  "connectorType": "string",
  "authModel": "None | ApiKey | OAuth | ManagedIdentity",
  "configuration": "string?",
  "managedApiId": "string?",
  "secret": "string?"
}
```

---

### Lấy danh sách connector
| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/connectors` | Không |

Trả về danh sách tất cả connector đã đăng ký trong hệ thống.

---

### Lấy chi tiết connector
| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/connectors/{connectorId}` | Không |

Lấy thông tin chi tiết của một connector theo ID.

---

### Xóa connector
| Method | Route | Auth |
|--------|-------|------|
| `DELETE` | `/connectors/{connectorId}` | Không |

Xóa connector khỏi hệ thống.

---

### Xác thực connector
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/connectors/{connectorId}/validate` | Không |

Kiểm tra tính hợp lệ của credentials/credentials cho connector (test kết nối).

---

### OAuth Callback
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/connectors/oauth/callback` | Không |

Nhận authorization code từ OAuth provider sau khi user đồng ý. Hệ thống dùng code này để đổi lấy access token và lưu vào connector credentials.

---

## Approvals

### Lấy danh sách approval đang chờ
| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/approvals/pending` | Không |

Trả về danh sách tất cả `ApprovalRequest` đang ở trạng thái **Pending**, bao gồm `Id`, `Title`, `Description`, `Status`, `ExpiresAt`.

---

### Xác nhận approval qua token (email link)
| Method | Route | Auth |
|--------|-------|------|
| `GET` | `/approvals/act?token=<jwt>` | Không |

Cho phép user approve/reject một yêu cầu phê duyệt thông qua đường link gửi qua email. Token JWT được giải mã để lấy `ApprovalRequestId` và `action` (`approve` hoặc `reject`). Không yêu cầu login.

---

## Webhooks

### Slack Webhook
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/webhooks/slack` | HMAC signature |

Nhận sự kiện từ Slack (ví dụ: phản hồi approval từ interactive message). Xác thực signing secret (`X-Slack-Signature` header). Xử lý qua `ProcessApprovalCommand`.

---

### Microsoft Teams Webhook
| Method | Route | Auth |
|--------|-------|------|
| `POST` | `/webhooks/teams` | HMAC signature |

Nhận sự kiện từ Microsoft Teams (ví dụ: phản hồi approval từ Adaptive Card). Xác thực HMAC signature (`Authorization: HMAC <hash>` header). Xử lý qua `ProcessApprovalCommand`.

---

## StepType Reference

| StepType | Mô tả |
|----------|-------|
| `Notification` | Gửi HTTP request đến URL endpoint (`configuration`) |
| `HumanApproval` | Tạm dừng workflow, chờ phê duyệt từ người dùng có `requiredRole` |
| `Action` | Thực thi một hành động tự động |
| `Condition` | Kiểm tra điều kiện rẽ nhánh |
