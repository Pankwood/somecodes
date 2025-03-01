﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Hosting;
using WebAPIBasicAuth.Models;

namespace WebAPIBasicAuth.BUS
{
    
    public class CustomAuthorizeAttribute : AuthorizeAttribute
    {
        private const string BasicAuthResponseHeader = "WWW-Authenticate";
        private const string BasicAuthResponseHeaderValue = "Basic";

        public string UsersConfigKey { get; set; }
        public string RolesConfigKey { get; set; }

        protected CustomPrincipal CurrentUser
        {
            get { return Thread.CurrentPrincipal as CustomPrincipal; }
            set { Thread.CurrentPrincipal = value as CustomPrincipal; }
        }

        //Sobreponho a classe responsavel por chamar a autorização de um request / HttpActionContext Contem informações da ação de requisição
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            HttpRequestMessage request = new HttpRequestMessage();
            try
            {
                //Representa informações de autenticação em Authorization, ProxyAuthorization, WWW-Authneticate, e valores de cabeçalho Proxy-Authenticate.
                AuthenticationHeaderValue authValue = actionContext.Request.Headers.Authorization;

                //Objeto de configuração que associa ao request
                request.Properties.Add(HttpPropertyKeys.HttpConfigurationKey, new HttpConfiguration());

                //Se não houver valor para authValue ele abre uma caixa de login
                if (authValue != null && !String.IsNullOrWhiteSpace(authValue.Parameter) && authValue.Scheme == BasicAuthResponseHeaderValue)
                {
                    //Contem as informações de usuario e senha. Ver método ParseAuthorizationHeader
                    Credentials parsedCredentials = ParseAuthorizationHeader(authValue.Parameter);

                    if (parsedCredentials != null)
                    {

                        //Identidade do usuário logado
                        CurrentUser = new CustomPrincipal(parsedCredentials.CPF, null);

                        CustomRoleProvider CurrentRole = new CustomRoleProvider();

                        //Aqui pode ser feita as consistências para validação do usuário
                        //if (!user.Contains(parsedCredentials.CPF))
                        if (!CurrentUser.GetUsers().Contains(parsedCredentials.CPF))
                        {
                            actionContext.Response = request.CreateResponse(HttpStatusCode.Unauthorized, "Serviço não permitido");
                            actionContext.Response.Headers.Add(BasicAuthResponseHeader, BasicAuthResponseHeaderValue);
                            return;
                        }

                        if (!CurrentUser.GetUsers(parsedCredentials.CPF, parsedCredentials.Senha))
                        {
                            actionContext.Response = request.CreateResponse(HttpStatusCode.Unauthorized, "Senha Incorreta");
                            actionContext.Response.Headers.Add(BasicAuthResponseHeader, BasicAuthResponseHeaderValue);
                            return;
                        }

                        if (!CurrentRole.IsUserInRole(parsedCredentials.CPF, Roles))
                        {
                            actionContext.Response = request.CreateResponse(HttpStatusCode.MethodNotAllowed, "Não tem permissão");
                            actionContext.Response.Headers.Add(BasicAuthResponseHeader, BasicAuthResponseHeaderValue);
                            return;
                        }


                       //Se passar pelas validações ele simplesmente da um return, caso contrário a aplicação para.
                       // actionContext.Response = request.CreateResponse(HttpStatusCode.Continue);
                       // actionContext.Response.Headers.Add(BasicAuthResponseHeader, BasicAuthResponseHeaderValue);
                        return;


                    }
                    else
                    {
                        actionContext.Response = request.CreateResponse(HttpStatusCode.Unauthorized, "Necessário efetuar login");
                        actionContext.Response.Headers.Add(BasicAuthResponseHeader, BasicAuthResponseHeaderValue);
                        return;
                    }
                }
                else
                {
                    actionContext.Response = request.CreateResponse(HttpStatusCode.Unauthorized, "Usuário não encontrado");
                    actionContext.Response.Headers.Add(BasicAuthResponseHeader, BasicAuthResponseHeaderValue);
                    return;
                }
            }
            catch (Exception)
            {
                actionContext.Response = request.CreateResponse(HttpStatusCode.InternalServerError);
                actionContext.Response.Headers.Add(BasicAuthResponseHeader, BasicAuthResponseHeaderValue);
                return;

            }
        }

        private Credentials ParseAuthorizationHeader(string authHeader)
        {
            //Decodifica a credencial que esta em Base64, e de acordo com a classe Credentials, atribui usuario e senha.
            string[] credentials = Encoding.ASCII.GetString(Convert.FromBase64String(authHeader)).Split(new[] { ':' });

            if (credentials.Length != 2 || string.IsNullOrEmpty(credentials[0]) || string.IsNullOrEmpty(credentials[1]))
                return null;

            return new Credentials() { CPF = credentials[0], Senha = credentials[1], };
        }
    }
    //Client credential
    public class Credentials
    {
        public string CPF { get; set; }
        public string Senha { get; set; }
    }

}