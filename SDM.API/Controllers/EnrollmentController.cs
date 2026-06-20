using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SDM.Application.DTOs.Enrollment;
using SDM.Infrastructure.Data;

namespace SDM.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class EnrollmentController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public EnrollmentController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Simple web enrollment page rendering minimal HTML form for device/browser enrollment
        [AllowAnonymous]
        [HttpGet("/enroll")]
        public IActionResult EnrollForm([FromQuery] string token)
        {
            var html = $@"<!doctype html>
<html>
<head><meta charset='utf-8'><title>Device Enrollment</title></head>
<body>
  <h1>Device Enrollment</h1>
  <p>Token: <strong>{System.Net.WebUtility.HtmlEncode(token)}</strong></p>
  <form id='enrollForm' method='post' action='/api/devices/register-with-token'>
    <input type='hidden' name='Token' value='{System.Net.WebUtility.HtmlEncode(token)}' />
    <label>Device Identifier: <input name='DeviceIdentifier' /></label><br/>
    <label>Serial Number: <input name='SerialNumber' /></label><br/>
    <label>Manufacturer: <input name='Manufacturer' /></label><br/>
    <label>Model: <input name='Model' /></label><br/>
    <label>Android Version: <input name='AndroidVersion' /></label><br/>
    <button type='submit'>Enroll</button>
  </form>
  <script>
    // Post form as JSON
    document.getElementById('enrollForm').addEventListener('submit', function(e){{
      e.preventDefault();
      const form = e.target;
      const data = {{
        token: form.Token.value,
        deviceIdentifier: form.DeviceIdentifier.value,
        serialNumber: form.SerialNumber.value,
        manufacturer: form.Manufacturer.value,
        model: form.Model.value,
        androidVersion: form.AndroidVersion.value
      }};
      fetch('/api/devices/register-with-token', {{
        method: 'POST', headers: {{ 'Content-Type':'application/json' }}, body: JSON.stringify(data)
      }}).then(r => r.json()).then(js => {{
        document.body.innerHTML = '<h2>Enrolled</h2><pre>'+JSON.stringify(js,null,2)+'</pre>';
      }}).catch(err => {{ alert('Enroll failed: '+err); }});
    }});
  </script>
</body>
</html>";

            return Content(html, "text/html");
        }

        [HttpPost("tokens")]
        public async Task<IActionResult> CreateToken([FromBody] CreateEnrollmentRequest request)
        {
            var token = new SDM.Domain.Entities.EnrollmentToken
            {
                Id = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString(),
                ExpiresOn = DateTime.UtcNow.AddMinutes(request.ExpiresInMinutes),
                MaxDevices = request.MaxDevices,
                IsActive = true
            };

            _db.EnrollmentTokens.Add(token);
            await _db.SaveChangesAsync();

            return Ok(new EnrollmentResponse
            {
                Token = token.Token,
                ExpiresOn = token.ExpiresOn,
                MaxDevices = token.MaxDevices
            });
        }

        [HttpPost("tokens/generate-qr")]
        public async Task<IActionResult> CreateTokenQr([FromBody] CreateEnrollmentRequest request)
        {
            var token = new SDM.Domain.Entities.EnrollmentToken
            {
                Id = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString(),
                ExpiresOn = DateTime.UtcNow.AddMinutes(request.ExpiresInMinutes),
                MaxDevices = request.MaxDevices,
                IsActive = true
            };

            _db.EnrollmentTokens.Add(token);
            await _db.SaveChangesAsync();

            // QR payload: deep link that opens the agent app
            var payload = "sdm://enroll?token=" + System.Uri.EscapeDataString(token.Token);

            using (var qrGenerator = new QRCoder.QRCodeGenerator())
            {
                var qrData = qrGenerator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCoder.PngByteQRCode(qrData))
                {
                    var qrBytes = qrCode.GetGraphic(20);
                    return File(qrBytes, "image/png", "enrollment-qr.png");
                }
            }
        }

        // Convenience anonymous endpoint for development/testing: generate QR without requiring auth
        [AllowAnonymous]
        [HttpPost("tokens/generate-qr/test")]
        public async Task<IActionResult> CreateTokenQrAnonymous([FromBody] CreateEnrollmentRequest request)
        {
            var token = new SDM.Domain.Entities.EnrollmentToken
            {
                Id = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString(),
                ExpiresOn = DateTime.UtcNow.AddMinutes(request.ExpiresInMinutes),
                MaxDevices = request.MaxDevices,
                IsActive = true
            };

            _db.EnrollmentTokens.Add(token);
            await _db.SaveChangesAsync();

            var payload = System.Text.Json.JsonSerializer.Serialize(new { server = Request.Scheme + "://" + Request.Host.Value, token = token.Token });

            using (var qrGenerator = new QRCoder.QRCodeGenerator())
            {
                var qrData = qrGenerator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCoder.PngByteQRCode(qrData))
                {
                    var qrBytes = qrCode.GetGraphic(20);
                    var base64 = Convert.ToBase64String(qrBytes);

                    // Return an HTML page showing the QR image and the raw token text for easy copy/paste during testing
                    var html = $@"<!doctype html>
<html>
<head><meta charset='utf-8'><title>Enrollment QR (test)</title></head>
<body>
  <h1>Enrollment QR (test)</h1>
  <p>Token: <strong>{System.Net.WebUtility.HtmlEncode(token.Token)}</strong></p>
  <p>Deep link: <code>sdm://enroll?token={System.Net.WebUtility.HtmlEncode(token.Token)}</code></p>
  <img src='data:image/png;base64,{base64}' alt='enrollment-qr' />
  <p>You can scan the QR with your device or copy the token above.</p>
</body>
</html>";

                    return Content(html, "text/html");
                }
            }
        }
    }
}
