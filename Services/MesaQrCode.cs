using QRCoder;
using C4Two.Models;

namespace C4Two.Services;

public static class MesaQrCode
{
    public static string Conteudo(MesaItem mesa)
    {
        return "c4two:mesa:" + mesa.IdEstabelecimento + ":" + mesa.Id + ":" + mesa.QrCode;
    }

    public static string PngBase64(MesaItem mesa)
    {
        if (string.IsNullOrWhiteSpace(mesa.QrCode))
        {
            return string.Empty;
        }

        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(Conteudo(mesa), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(dados);
        return Convert.ToBase64String(png.GetGraphic(4));
    }
}
