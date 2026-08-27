using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Store.Web.Infrastructure.Uploads;

namespace EndToEndTests;

/// <summary>
/// Pure filesystem tests for <see cref="LocalProductImageStorage"/>'s delete paths — no database and
/// no <c>StoreWebApplicationFactory</c>, so this class does not participate in the shared-database
/// serialization the rest of this project needs (see the csproj comment). It lives here rather than
/// in UnitTests only because UnitTests deliberately does not reference Store.Web.
///
/// The traversal assertions matter more than they look: <c>Delete</c> takes a URL read back from the
/// database, and a delete that escapes the uploads directory would let a bad stored value reach
/// arbitrary files under wwwroot.
/// </summary>
public sealed class LocalProductImageStorageTests : IDisposable
{
    private readonly string _webRoot;
    private readonly LocalProductImageStorage _storage;

    public LocalProductImageStorageTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "posflow-image-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);
        _storage = new LocalProductImageStorage(new StubWebHostEnvironment(_webRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }

    private string CreateUploadedFile(Guid productId, string fileName)
    {
        var directory = Path.Combine(_webRoot, "uploads", "products", productId.ToString());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "image bytes");
        return path;
    }

    [Fact]
    public void Delete_removes_the_file_behind_a_stored_url()
    {
        var productId = Guid.NewGuid();
        var path = CreateUploadedFile(productId, "photo.jpg");

        var deleted = _storage.Delete($"/uploads/products/{productId}/photo.jpg");

        deleted.Should().BeTrue();
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Delete_reports_success_when_the_file_is_already_gone()
    {
        // The database row is the source of truth; a missing file means the desired end state is
        // already reached, so this must not surface as a failure to the admin.
        var deleted = _storage.Delete($"/uploads/products/{Guid.NewGuid()}/never-existed.jpg");

        deleted.Should().BeTrue();
    }

    [Theory]
    // The first case is the one with teeth: it resolves to exactly the file created below, so if the
    // traversal guard is ever removed this test fails on a real deletion rather than on a return
    // value. The rest cover neighbouring shapes (deeper escapes, a sibling wwwroot folder).
    [InlineData("/uploads/products/../../outside.txt")]
    [InlineData("/uploads/products/../../../secrets.txt")]
    [InlineData("/../outside.txt")]
    [InlineData("/css/site.css")]
    [InlineData("/uploads/other-thing/file.jpg")]
    public void Delete_refuses_urls_that_resolve_outside_the_uploads_root(string url)
    {
        var outsideFile = Path.Combine(_webRoot, "outside.txt");
        File.WriteAllText(outsideFile, "must survive");

        var deleted = _storage.Delete(url);

        deleted.Should().BeFalse();
        File.Exists(outsideFile).Should().BeTrue("a URL outside the uploads root must never delete anything");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Delete_refuses_a_blank_url(string url)
    {
        _storage.Delete(url).Should().BeFalse();
    }

    [Fact]
    public void DeleteAllForProduct_removes_every_image_and_the_folder()
    {
        var productId = Guid.NewGuid();
        CreateUploadedFile(productId, "one.jpg");
        CreateUploadedFile(productId, "two.png");
        var directory = Path.Combine(_webRoot, "uploads", "products", productId.ToString());

        _storage.DeleteAllForProduct(productId);

        Directory.Exists(directory).Should().BeFalse();
    }

    [Fact]
    public void DeleteAllForProduct_leaves_other_products_untouched()
    {
        var deleted = Guid.NewGuid();
        var kept = Guid.NewGuid();
        CreateUploadedFile(deleted, "gone.jpg");
        var keptPath = CreateUploadedFile(kept, "stays.jpg");

        _storage.DeleteAllForProduct(deleted);

        File.Exists(keptPath).Should().BeTrue();
    }

    [Fact]
    public void DeleteAllForProduct_is_a_no_op_for_a_product_with_no_uploads()
    {
        var act = () => _storage.DeleteAllForProduct(Guid.NewGuid());

        act.Should().NotThrow();
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string webRootPath) => WebRootPath = webRootPath;

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = nameof(LocalProductImageStorageTests);

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public string EnvironmentName { get; set; } = "Test";
    }
}
