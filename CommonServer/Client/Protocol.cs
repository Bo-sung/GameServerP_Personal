using System.Text;

public class Protocol
{
    public enum ProtocolType
    {
        ToServer,   // 서버로 발송
        ToClient,   // 클라이언트로 발송
        Both        // 양쪽 다 사용 가능
    }

    public readonly int ID;
    public readonly Dictionary<int, object> Parameter = new Dictionary<int, object>();
    public ProtocolType protocolType;


    public Protocol(int iD, ProtocolType protocolType)
    {
        ID = iD;
        this.protocolType = protocolType;
    }

    public void SetParam(int key, object value)
    {
        if (Parameter.ContainsKey(key))
            Parameter[key] = value;
        else
            Parameter.Add(key, value);
    }

    public const string STREAM_TOK = "!@";
    public const string STREAM_END = "#@@";

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(ID);
        foreach (var item in Parameter)
        {
            stringBuilder.Append(STREAM_TOK);
            stringBuilder.Append(item.Key);
            stringBuilder.Append(STREAM_TOK);
            stringBuilder.Append(item.Value);
        }
        stringBuilder.Append(STREAM_END);
        return stringBuilder.ToString();
    }

    public Dictionary<int, object> FromString(string str)
    {
        Dictionary<int, object> result = new Dictionary<int, object>();
        string[] temp = str.Split(STREAM_TOK);

        for (int i = 0; i < temp.Length;)
        {
            if (int.TryParse(temp[i], out int key))
            {
                result.Add(key, temp[i + 1]);
            }

            i = i + 2;
        }

        return result;
    }

    public static class IDs
    {
        public const int STREAM_START = 0;
        public const int USER_ENTER_SUCCESS = 1;

        public const int USER_LOGIN_REQUEST = 100;
        public const int USER_LOGOUT_REQUEST = 101;
        public const int PING_REQUEST = 102;

        public const int USER_LOGIN_SUCCESS = 200;
        public const int USER_LOGIN_FAIL = 201;
        public const int USER_LOGOUT_SUCCESS = 202;
        public const int PONG_RESPONSE = 203;
    }

    public static Protocol USER_ENTER_SUCCESS       = new Protocol(IDs.USER_ENTER_SUCCESS       , ProtocolType.ToClient);

    // 새로 추가된 프로토콜들
    // 클라이언트 -> 서버
    public static Protocol USER_LOGIN_REQUEST       = new Protocol(IDs.USER_LOGIN_REQUEST       , ProtocolType.ToServer);
    public static Protocol USER_LOGOUT_REQUEST      = new Protocol(IDs.USER_LOGOUT_REQUEST      , ProtocolType.ToServer);
    public static Protocol PING_REQUEST             = new Protocol(IDs.PING_REQUEST             , ProtocolType.ToServer);

    // 서버 -> 클라이언트
    public static Protocol USER_LOGIN_SUCCESS       = new Protocol(IDs.USER_LOGIN_SUCCESS       , ProtocolType.ToClient);
    public static Protocol USER_LOGIN_FAIL          = new Protocol(IDs.USER_LOGIN_FAIL          , ProtocolType.ToClient);
    public static Protocol USER_LOGOUT_SUCCESS      = new Protocol(IDs.USER_LOGOUT_SUCCESS      , ProtocolType.ToClient);
    public static Protocol PONG_RESPONSE            = new Protocol(IDs.PONG_RESPONSE            , ProtocolType.ToClient);
}
