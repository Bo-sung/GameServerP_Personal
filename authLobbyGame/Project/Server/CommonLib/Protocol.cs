using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace CommonLib
{
    public enum StateCode
    {
        SUCCESS = 0,            // 성공
        FAIL = 1,               // 실패
        AUTH_FAILURE = 2,       // 인증 실패
        ACCESS_DENY = 3,        // 권한 부족
        NO_RESOURCE = 4,        // 리소스 없음
        SERVER_ERROR = 5,       // 서버 에러
    }
    public class Response : Protocol
    {
        public Response(int responseId, StateCode status, string message = "") : base(ProtocolType.RESPONSE)
        {
            AddParam("protoId", responseId);
            AddParam("status", (byte)status);
            if (message == "")
                message = status.ToString();
            AddParam("message", message);
        }
    }

    public class Protocol
    {
        public int Type { get; set; }
        public long Timestamp { get; set; }
        private Dictionary<string, object> _parameters = new Dictionary<string, object>();

        public Protocol(int type)
        {
            Type = type;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public Protocol AddParam(string key, object value)
        {
            _parameters[key] = value;
            return this;
        }

        /// <summary>
        /// 구조체 추가 (서버 호환성)
        /// JSON으로 직렬화하여 저장
        /// </summary>
        public Protocol AddStruct<T>(string key, T value) where T : struct
        {
            string json = JsonConvert.SerializeObject(value);
            _parameters[key] = json;
            return this;
        }

        /// <summary>
        /// 객체/클래스 추가 (서버 호환성)
        /// JSON으로 직렬화하여 저장
        /// </summary>
        public Protocol AddObject<T>(string key, T value) where T : class
        {
            if (value == null)
            {
                _parameters[key] = null;
                return this;
            }
            string json = JsonConvert.SerializeObject(value);
            _parameters[key] = json;
            return this;
        }

        public T GetParam<T>(string key)
        {
            if (_parameters.TryGetValue(key, out object value))
            {
                if (value is T directValue)
                    return directValue;

                // Newtonsoft.Json.Linq 타입 처리
                if (value is Newtonsoft.Json.Linq.JToken jToken)
                {
                    try
                    {
                        return jToken.ToObject<T>();
                    }
                    catch
                    {
                        // 변환 실패 시 계속 진행
                    }
                }

                // JSON 문자열인 경우 역직렬화 시도
                if (value is string jsonString && typeof(T) != typeof(string))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(jsonString);
                    }
                    catch
                    {
                        // 역직렬화 실패 시 기본값 반환
                    }
                }

                // 타입 변환 시도
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // 변환 실패 시 기본값 반환
                }
            }
            return default(T);
        }

        public T GetStruct<T>(string key) where T : struct
        {
            if (_parameters.TryGetValue(key, out object value))
            {
                // Newtonsoft.Json.Linq 타입 처리
                if (value is Newtonsoft.Json.Linq.JToken jToken)
                {
                    try
                    {
                        return jToken.ToObject<T>();
                    }
                    catch
                    {
                        // 변환 실패 시 계속 진행
                    }
                }

                if (value is string jsonString)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(jsonString);
                    }
                    catch
                    {
                        // 역직렬화 실패 시 기본값 반환
                    }
                }

                // 직접 타입인 경우
                if (value is T directValue)
                    return directValue;
            }
            return default(T);
        }

        /// <summary>
        /// 객체/클래스 가져오기 (서버 호환성)
        /// </summary>
        public T GetObject<T>(string key) where T : class
        {
            if (_parameters.TryGetValue(key, out object value))
            {
                if (value == null)
                    return null;

                // Newtonsoft.Json.Linq 타입 처리
                if (value is Newtonsoft.Json.Linq.JToken jToken)
                {
                    try
                    {
                        return jToken.ToObject<T>();
                    }
                    catch
                    {
                        // 변환 실패 시 계속 진행
                    }
                }

                if (value is string jsonString)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(jsonString);
                    }
                    catch
                    {
                        // 역직렬화 실패 시 null 반환
                    }
                }

                // 직접 타입인 경우
                if (value is T directValue)
                    return directValue;
            }
            return null;
        }

        /// <summary>
        /// 바이트 배열 가져오기 (서버 호환성)
        /// </summary>
        public byte[] GetBytes(string key)
        {
            if (_parameters.TryGetValue(key, out object value))
            {
                return value as byte[];
            }
            return null;
        }

        /// <summary>
        /// 파라미터 존재 여부 확인 (서버 호환성)
        /// </summary>
        public bool HasParam(string key)
        {
            return _parameters.ContainsKey(key);
        }

        public byte[] Serialize()
        {
            // 간단한 바이너리 직렬화 구현
            string jsonData = JsonConvert.SerializeObject(_parameters);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

            byte[] result = new byte[18 + jsonBytes.Length]; // 헤더(18) + 데이터

            // 전체 크기 (4바이트) - 서버는 헤더를 포함한 전체 크기를 사용함
            BitConverter.GetBytes(result.Length).CopyTo(result, 0);

            // 프로토콜 타입 (4바이트)
            BitConverter.GetBytes(Type).CopyTo(result, 4);

            // 타임스탬프 (8바이트)
            BitConverter.GetBytes(Timestamp).CopyTo(result, 8);

            // 데이터 개수 (2바이트)
            BitConverter.GetBytes((ushort)_parameters.Count).CopyTo(result, 16);

            // JSON 데이터
            jsonBytes.CopyTo(result, 18);

            return result;
        }

        public Dictionary<string, object> GetParams()
        {
            return _parameters;
        }

        public static Protocol Deserialize(byte[] data)
        {
            if (data.Length < 18) throw new ArgumentException("Invalid protocol data");

            // 크기 필드 읽기 (헤더 4바이트 제외한 크기)
            int messageSize = BitConverter.ToInt32(data, 0);
            int type = BitConverter.ToInt32(data, 4);
            long timestamp = BitConverter.ToInt64(data, 8);
            ushort paramCount = BitConverter.ToUInt16(data, 16);

            Protocol protocol = new Protocol(type) { Timestamp = timestamp };

            // JSON 길이 계산
            // 서버가 보내는 messageSize는 크기 필드 자신(4)을 포함
            // messageSize = 크기(4) + 타입(4) + 타임스탬프(8) + 데이터개수(2) + JSON
            // JSON 길이 = messageSize - 18
            int jsonLength = messageSize - 18;

            if (jsonLength > 0 && data.Length >= 18 + jsonLength)
            {
                // 정확한 길이만큼만 JSON 파싱
                string jsonData = Encoding.UTF8.GetString(data, 18, jsonLength);

                try
                {
                    var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);
                    if (parameters != null)
                    {
                        protocol._parameters = parameters;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Protocol] JSON 파싱 오류: {ex.Message}\nJSON: {jsonData}\n크기: messageSize={messageSize}, jsonLength={jsonLength}, dataLength={data.Length}");
                    throw;
                }
            }

            return protocol;
        }
    }
}
