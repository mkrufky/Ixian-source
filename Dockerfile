FROM mono:5 AS build
RUN apt-get update && apt-get upgrade -y
RUN apt-get install -y git make gcc

RUN mkdir -p /Ixian-source
WORKDIR /Ixian-source

COPY . .

WORKDIR /Ixian-source/IxianDLT

RUN nuget restore DLTNode.sln
RUN msbuild DLTNode.sln /p:Configuration=Release

WORKDIR /

RUN git clone https://github.com/P-H-C/phc-winner-argon2.git
WORKDIR /phc-winner-argon2

RUN make install PREFIX=/Ixian-source/IxianDLT/bin/Release

# runtime image
FROM mono:5
RUN apt-get update && apt-get upgrade -y

COPY --from=build /Ixian-source/IxianDLT/bin/Release/* /

CMD [ "mono",  "./IxianDLT.exe" ]
