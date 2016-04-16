using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using Frapid.Areas.Authorization;
using Frapid.Config.Models;
using Frapid.Configuration;
using Frapid.Dashboard;
using Frapid.Dashboard.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Frapid.Config.Controllers
{
    public class FileManagerController : DashboardController
    {
        [Route("dashboard/config/file-manager")]
        [RestrictAnonymous]
        [MenuPolicy]
        public ActionResult Index()
        {
            return this.FrapidView(this.GetRazorView<AreaRegistration>("FileManager/Index.cshtml"));
        }

        [Route("dashboard/config/file-manager/resources")]
        [RestrictAnonymous]
        public ActionResult GetResources()
        {
            string tenant = DbConvention.GetTenant();
            string path = $"~/Tenants/{tenant}/";
            path = HostingEnvironment.MapPath(path);

            if (path == null || !Directory.Exists(path))
            {
                return this.Failed("Path not found", HttpStatusCode.NotFound);
            }

            var resource = FileManagerResource.Get(path);
            resource = FileManagerResource.NormalizePath(path, resource);

            string json = JsonConvert.SerializeObject(resource, Formatting.None,
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

            return this.Content(json, "application/json");
        }

        [Route("dashboard/config/file-manager/resources/edit/file")]
        [RestrictAnonymous]
        [HttpPut]
        public ActionResult EditFile(string themeName, string container, string file, string contents)
        {
            return this.CreateResource(container, file, false, contents, true);
        }

        [Route("dashboard/config/file-manager/create/file")]
        [RestrictAnonymous]
        [HttpPut]
        public ActionResult CreateFile(string container, string file, string contents)
        {
            return this.CreateResource(container, file, false, contents);
        }

        [Route("dashboard/config/file-manager/create/folder")]
        [RestrictAnonymous]
        [HttpPut]
        public ActionResult CreateFolder(string container, string folder)
        {
            return this.CreateResource(container, folder, true, null);
        }

        [Route("dashboard/config/file-manager/delete")]
        [RestrictAnonymous]
        [HttpDelete]
        public ActionResult DeleteResource(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                return this.AccessDenied();
            }

            try
            {
                var remover = new ResourceRemover(resource);
                remover.Delete();
            }
            catch (ResourceRemoveException ex)
            {
                return this.Failed(ex.Message, HttpStatusCode.InternalServerError);
            }

            return this.Ok();
        }

        private ActionResult CreateResource(string container, string file, bool isDirectory,
            string contents, bool rewriteFile = false)
        {
            try
            {
                var resource = new ResourceCreator
                {
                    Container = container,
                    File = file,
                    IsDirectory = isDirectory,
                    Contents = contents,
                    Rewrite = rewriteFile
                };

                resource.Create();
            }
            catch (ResourceCreateException ex)
            {
                return this.Failed(ex.Message, HttpStatusCode.InternalServerError);
            }

            return this.Ok();
        }

        [Route("dashboard/config/file-manager/resources/upload")]
        [RestrictAnonymous]
        [HttpPost]
        public ActionResult UploadResource(string container)
        {
            if (this.Request.Files.Count > 1)
            {
                return this.Failed("Only single file may be uploaded", HttpStatusCode.BadRequest);
            }

            var file = this.Request.Files[0];
            if (file == null)
            {
                return this.Failed("No file was uploaded", HttpStatusCode.BadRequest);
            }

            try
            {
                var uploader = new ResourceUploader(file, container);
                uploader.Upload();
            }
            catch (ResourceUploadException ex)
            {
                return this.Failed(ex.Message, HttpStatusCode.InternalServerError);
            }

            return this.Ok();
        }

        [Route("dashboard/config/file-manager/blob")]
        [RestrictAnonymous]
        public ActionResult GetBinary(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return this.HttpNotFound();
            }

            string tenant = DbConvention.GetTenant();
            string path = HostingEnvironment.MapPath($"/Tenants/{tenant}/{file}");

            if (!System.IO.File.Exists(path))
            {
                return this.HttpNotFound();
            }

            string mimeType = this.GetMimeType(path);
            return this.File(path, mimeType);
        }


        [Route("dashboard/config/file-manager/{*resource}")]
        [RestrictAnonymous]
        public ActionResult Get(string resource = "")
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                return this.HttpNotFound();
            }

            string tenant = DbConvention.GetTenant();

            var allowed = FrapidConfig.GetMyAllowedResources(tenant);

            if (string.IsNullOrWhiteSpace(resource) || allowed.Count().Equals(0))
            {
                return this.HttpNotFound();
            }

            string directory = HostingEnvironment.MapPath($"/Tenants/{tenant}");

            if (directory == null)
            {
                return this.HttpNotFound();
            }

            string path = Path.Combine(directory, resource);

            if (!System.IO.File.Exists(path))
            {
                return this.HttpNotFound();
            }

            string extension = Path.GetExtension(path);

            if (!allowed.Contains(extension))
            {
                return this.HttpNotFound();
            }

            string mimeType = this.GetMimeType(path);
            return this.File(path, mimeType);
        }

        private string GetMimeType(string fileName)
        {
            return MimeMapping.GetMimeMapping(fileName);
        }
    }
}