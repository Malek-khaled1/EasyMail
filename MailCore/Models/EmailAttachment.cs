namespace MailCore.Models
{
    /// <summary>
    /// Repræsenterer en vedhæftet fil til en e-mail.
    /// Understøtter alle filtyper (png, pdf, docx, zip osv.).
    /// Ren data-model, ingen IO eller UI.
    /// </summary>
    public class EmailAttachment
    {
        /// <summary>
        /// Det filnavn modtageren ser (fx "invoice.pdf").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// MIME-type, fx:
        /// - "application/pdf"
        /// - "image/png"
        /// - "application/zip"
        /// - "application/vnd.openxmlformats-officedocument.wordprocessingml.document" (docx)
        ///
        /// Hvis du er i tvivl, kan du sætte "application/octet-stream"
        /// og lade transportlaget (MailKit/Gmail) gætte ud fra filnavn.
        /// </summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// Selve filens indhold som byte-array.
        /// </summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}
