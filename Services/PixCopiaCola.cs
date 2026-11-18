using System.Globalization;
using System.Text;
using QRCoder;

namespace C4Two.Services;

public static class PixCopiaCola
{
    public static string Montar(string chave, string recebedor, string cidade, decimal valor)
    {
        var conta = Campo("00", "br.gov.bcb.pix") + Campo("01", string.IsNullOrWhiteSpace(chave) ? "pagamento@c4two.com" : chave.Trim());
        var semCrc =
            Campo("00", "01")
            + Campo("26", conta)
            + Campo("52", "0000")
            + Campo("53", "986")
            + Campo("54", valor.ToString("0.00", CultureInfo.InvariantCulture))
            + Campo("58", "BR")
            + Campo("59", Limpar(recebedor, 25, "C4Two"))
            + Campo("60", Limpar(cidade, 15, "Brasil"))
            + Campo("62", Campo("05", "***"));
        var baseCrc = semCrc + "6304";
        return baseCrc + Crc16(baseCrc);
    }

    public static string PngBase64(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return string.Empty;
        }

        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.Q);
        return Convert.ToBase64String(new PngByteQRCode(dados).GetGraphic(4));
    }

    private static string Campo(string id, string valor)
    {
        var texto = valor ?? string.Empty;
        return id + texto.Length.ToString("D2", CultureInfo.InvariantCulture) + texto;
    }

    private static string Limpar(string? texto, int maximo, string reserva)
    {
        var origem = string.IsNullOrWhiteSpace(texto) ? reserva : texto.Trim();
        var normalizado = origem.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var caractere in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caractere) || caractere == ' ')
            {
                sb.Append(caractere);
            }
        }

        var limpo = sb.ToString().Trim();
        if (limpo.Length == 0)
        {
            limpo = reserva;
        }

        return limpo.Length <= maximo ? limpo : limpo[..maximo];
    }

    private static string Crc16(string texto)
    {
        var crc = 0xFFFF;
        foreach (var byteValor in Encoding.UTF8.GetBytes(texto))
        {
            crc ^= byteValor << 8;
            for (var bit = 0; bit < 8; bit++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (crc << 1) ^ 0x1021;
                }
                else
                {
                    crc <<= 1;
                }

                crc &= 0xFFFF;
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
