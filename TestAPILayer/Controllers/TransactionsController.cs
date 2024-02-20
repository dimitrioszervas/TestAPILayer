using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Text;

namespace TestAPILayer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private static readonly FormOptions _defaultFormOptions = new FormOptions();

        /// <summary>
        /// Return the uploaded file enconding.
        /// </summary>
        /// <param name="section"></param>
        /// <returns>Encoding</returns>
        private Encoding GetEncoding(MultipartSection section)
        {
            MediaTypeHeaderValue mediaType;
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out mediaType);
            // UTF-7 is insecure and should not be honored. UTF-8 will succeed in 
            // most cases.
         
            if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
            {
                return Encoding.UTF8;
            }
            return mediaType.Encoding;
        }

        [HttpPost]
        [Route("PostTransaction")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> PostTransaction()
        {
            //string body = "";
            //using (var reader = new StreamReader(Request.Body))
            //{
            //    body = await reader.ReadToEndAsync();
            //    Console.WriteLine(body);
            //}
            Console.WriteLine("Entered PostTransaction");

            if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
            {
                ModelState.AddModelError("File",
                    $"The request couldn't be processed (Error 1).");
                // Log error

                return BadRequest(ModelState);
            }

            string boundary = MultipartRequestHelper.GetBoundary(
                MediaTypeHeaderValue.Parse(Request.ContentType),
                _defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);
            var section = await reader.ReadNextSectionAsync();
         
            byte[] fileBytes = null;
            string transactionString = null;

            string value = "";
            while (section != null)
            {               
                var hasContentDispositionHeader =
                    ContentDispositionHeaderValue.TryParse(
                        section.ContentDisposition, out var contentDisposition);
               
                if (hasContentDispositionHeader)
                {                 

                    if (!ModelState.IsValid)
                    {
                        return BadRequest(ModelState);
                    }
                   
                    var encoding = GetEncoding(section);
                    using (var streamReader = new StreamReader(
                        section.Body,
                        encoding,
                        detectEncodingFromByteOrderMarks: true,
                        leaveOpen: false))
                    {
                       
                        value = await streamReader.ReadToEndAsync();                      
                       
                    }
                    
                }

                // Drain any remaining section body that hasn't been consumed and
                // read the headers for the next section.
                section = await reader.ReadNextSectionAsync();     
            }
            Console.WriteLine(value);

            return Ok("Success!");
        }
    }
}
