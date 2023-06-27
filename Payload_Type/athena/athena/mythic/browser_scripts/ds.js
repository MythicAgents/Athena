function(task, responses){
    if(task.status.includes("error")){
        var combined = responses.reduce( (prev, cur) => {
            return prev + cur;
        }, "");
        return {'plaintext': combined};
    }else if(responses.length > 0){
        var task_output = "";
        for(let i = 0; i < responses.length; i++)
        {
            var task_data = responses[i];
            try{
                var data = JSON.parse(task_data);
            }catch(error){
               var combined = responses.reduce( (prev, cur) => {
                    return prev + cur;
                }, "");
                return {'plaintext': combined};
            }

            data.forEach(item => 
            {
                task_output += item["DistinguishedName"] + "\n";
                var attribs = item["Attributes"];
                var attribKeys = Object.keys(attribs);
                attribKeys.forEach(function(key){
                    var value = "";
                    var attribute = attribs[key];
                    if(key == "objectguid"){
                        var binaryString = atob(attribute[0]);
                        var bytes = new Uint8Array(binaryString.length);
                        for (var i = 0; i < binaryString.length; i++) {
                            bytes[i] = binaryString.charCodeAt(i);
                        }
                        //var arrayBuffer = ArrayBuffer.from(bytes);
                        var dataView = new DataView(bytes.buffer);
                        var guid = [
                            dataView.getUint32(0, true),
                            dataView.getUint16(4, true),
                            dataView.getUint16(6, true),
                            dataView.getUint8(8),
                            dataView.getUint8(9),
                            dataView.getUint8(10),
                            dataView.getUint8(11),
                            dataView.getUint8(12),
                            dataView.getUint8(13),
                            dataView.getUint8(14),
                            dataView.getUint8(15)
                          ].map(byte => byte.toString(16).padStart(2, '0'));
                        var guidString = `${guid[3]}${guid[2]}${guid[1]}${guid[0]}-${guid[5]}${guid[4]}-${guid[7]}${guid[6]}-${guid[8]}${guid[9]}-${guid.slice(10).join('')}`;
                        value = guidString;
                    }
                    else if(key == "objectsid"){
                        let pad = function(s) { if (s.length < 2) { return `0${s}`; } else { return s; } };
                        var binaryString = atob(attribute[0]);
                        var buf = new Uint8Array(binaryString.length);
                        for (var i = 0; i < binaryString.length; i++) {
                            buf[i] = binaryString.charCodeAt(i);
                        }
                        
                        var asc, end;
                        var i;
                        
                        var version = buf[0];
                        var subAuthorityCount   = buf[1];
                        var identifierAuthority = parseInt(((() => {
                            var result = [];
                            for (i = 2; i <= 7; i++) {
                              result.push(buf[i].toString(16));
                            }
                            return result;
                        })()).join(''), 16);
                        
                        var sidString = `S-${version}-${identifierAuthority}`;
                        
                        for (i = 0, end = subAuthorityCount-1, asc = 0 <= end; asc ? i <= end : i >= end; asc ? i++ : i--) {
                            var subAuthOffset = i * 4;
                            var tmp =
                            pad(buf[11 + subAuthOffset].toString(16)) +
                            pad(buf[10 + subAuthOffset].toString(16)) +
                            pad(buf[9  + subAuthOffset].toString(16)) +
                            pad(buf[8  + subAuthOffset].toString(16));
                            sidString += `-${parseInt(tmp, 16)}`;
                        }
                        
                        value = sidString;
                        //var binaryString = atob(attribute[0]);
                        //var revision = binaryString.charCodeAt(0);
                        //var subAuthorityCount = binaryString.charCodeAt(1);
                        //var subAuthorities = [];
                        //for (let i = 0; i < subAuthorityCount; i++) {
                        //    var start = 2 + i * 4;
                        //    var subAuthorityBytes = binaryString.slice(start, start + 4);
                        //    var subAuthorityValue = (subAuthorityBytes.charCodeAt(0))
                        //      + (subAuthorityBytes.charCodeAt(1) << 8)
                        //      + (subAuthorityBytes.charCodeAt(2) << 16)
                        //      + (subAuthorityBytes.charCodeAt(3) << 24);
                        //    subAuthorities.push(subAuthorityValue);
                        //}
                        //var objectSid = `S-${revision}-${subAuthorities.join('')}`;
                        //value = objectSid + " " + attribute[0]; 
                    }
                    else if(key == "pwdlastset" || key == "lastlogontimestamp" || key == "lastlogon" || key == "badpasswordtime" || key == "accountexpires"){
                        var binaryString = atob(attribute[0]);
                        var decimalValue = binaryString.charCodeAt(0)
                            + (binaryString.charCodeAt(1) << 8)
                            + (binaryString.charCodeAt(2) << 16)
                            + (binaryString.charCodeAt(3) << 24);
                        
                        var date = new Date(binaryString/1e4-1.16444736e13);
                        value = date.toISOString();
                        
                    }
                    else if (key == "memberof" || key == "member"){
                        //value += "\n"
                    attribute.forEach(function(attr){
                        value += "\n\t\t" + atob(attr) + " "; 
                    });  
                    }
                    else if(key == "whencreated" || key == "whenchanged" || key == "dscorepropagationdata" ){
                        attribute.forEach(function(attr){
                            var fullstamp = atob(attr)
                            var timestamp = fullstamp.split('.')[0]
                            var year = timestamp.substr(0, 4);
                            var month = timestamp.substr(4, 2) - 1; // JavaScript months are zero-based
                            var day = timestamp.substr(6, 2);
                            var hour = timestamp.substr(8, 2);
                            var minute = timestamp.substr(10, 2);
                            var second = timestamp.substr(12, 2);
                            var date = new Date(year, month, day, hour, minute, second);
                            value += "\n\t\t" + date.toISOString() + " "; 
                        }); 
                    }
                    else{
                       attribute.forEach(function(attr){
                       value += atob(attr) + " "; 
                    });  
                    }
                    task_output += "    " + key + ": " + value + "\n";
                });
                task_output += "\n";
            });


        }
        return {"plaintext" : task_output};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}
